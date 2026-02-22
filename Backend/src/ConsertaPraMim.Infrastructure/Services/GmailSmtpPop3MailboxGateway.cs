using System.Text.RegularExpressions;
using ConsertaPraMim.Application.Interfaces;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ConsertaPraMim.Infrastructure.Services;

public class GmailSmtpPop3MailboxGateway : IAdminMailboxGateway
{
    public async Task SendAsync(AdminMailboxGatewaySendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var connection = request.Connection ?? throw new ArgumentNullException(nameof(request.Connection));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            connection.SenderDisplayName ?? connection.SenderEmail,
            connection.SenderEmail));
        message.To.Add(MailboxAddress.Parse(request.To));
        message.Subject = request.Subject;
        var bodyBuilder = new BodyBuilder();
        if (request.IsHtml)
        {
            bodyBuilder.HtmlBody = request.Body;
        }
        else
        {
            bodyBuilder.TextBody = request.Body;
        }

        if (request.Attachments is { Count: > 0 })
        {
            foreach (var attachment in request.Attachments)
            {
                if (attachment.ContentBytes is not { Length: > 0 })
                {
                    continue;
                }

                var fileName = string.IsNullOrWhiteSpace(attachment.FileName)
                    ? $"anexo-{Guid.NewGuid():N}.bin"
                    : attachment.FileName.Trim();

                if (string.IsNullOrWhiteSpace(attachment.ContentType))
                {
                    bodyBuilder.Attachments.Add(fileName, attachment.ContentBytes);
                    continue;
                }

                try
                {
                    var parsedContentType = ContentType.Parse(attachment.ContentType.Trim());
                    bodyBuilder.Attachments.Add(fileName, attachment.ContentBytes, parsedContentType);
                }
                catch
                {
                    bodyBuilder.Attachments.Add(fileName, attachment.ContentBytes);
                }
            }
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var smtpClient = new SmtpClient();
        await smtpClient.ConnectAsync(
            connection.SmtpHost,
            connection.SmtpPort,
            connection.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
            cancellationToken);
        await smtpClient.AuthenticateAsync(connection.Username, connection.Password, cancellationToken);
        await smtpClient.SendAsync(message, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminMailboxGatewayInboundMessage>> FetchInboundAsync(
        AdminMailboxGatewayFetchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var connection = request.Connection ?? throw new ArgumentNullException(nameof(request.Connection));
        var take = request.Take <= 0 ? 40 : Math.Min(request.Take, 400);

        using var pop3Client = new Pop3Client();
        await pop3Client.ConnectAsync(connection.Pop3Host, connection.Pop3Port, connection.Pop3UseSsl, cancellationToken);
        await pop3Client.AuthenticateAsync(connection.Username, connection.Password, cancellationToken);

        var count = pop3Client.Count;
        if (count == 0)
        {
            await pop3Client.DisconnectAsync(true, cancellationToken);
            return Array.Empty<AdminMailboxGatewayInboundMessage>();
        }

        var startIndex = Math.Max(0, count - take);
        var items = new List<AdminMailboxGatewayInboundMessage>(Math.Min(count, take));

        for (var index = count - 1; index >= startIndex; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uid = await pop3Client.GetMessageUidAsync(index, cancellationToken);
            var mime = await pop3Client.GetMessageAsync(index, cancellationToken);
            var fromAddress = mime.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
            var toAddress = mime.To.Mailboxes.FirstOrDefault()?.Address ?? connection.SenderEmail;
            var subject = string.IsNullOrWhiteSpace(mime.Subject) ? "(sem assunto)" : mime.Subject.Trim();
            var bodyText = mime.TextBody ?? StripHtml(mime.HtmlBody);
            var bodyHtml = string.IsNullOrWhiteSpace(mime.HtmlBody) ? null : mime.HtmlBody;
            var occurredAtUtc = mime.Date == DateTimeOffset.MinValue
                ? DateTime.UtcNow
                : mime.Date.UtcDateTime;

            items.Add(new AdminMailboxGatewayInboundMessage(
                ExternalMessageId: !string.IsNullOrWhiteSpace(uid) ? uid.Trim() : (mime.MessageId ?? Guid.NewGuid().ToString("N")),
                Subject: subject,
                FromAddress: fromAddress,
                ToAddress: toAddress,
                BodyText: bodyText ?? string.Empty,
                BodyHtml: bodyHtml,
                OccurredAtUtc: occurredAtUtc));
        }

        await pop3Client.DisconnectAsync(true, cancellationToken);
        return items;
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutTags = Regex.Replace(html, "<[^>]+>", " ", RegexOptions.Compiled);
        var normalized = withoutTags
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase);
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Trim();
    }
}
