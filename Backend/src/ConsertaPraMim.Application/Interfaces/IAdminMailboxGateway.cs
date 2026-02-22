namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminMailboxGateway
{
    Task SendAsync(AdminMailboxGatewaySendRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminMailboxGatewayInboundMessage>> FetchInboundAsync(
        AdminMailboxGatewayFetchRequest request,
        CancellationToken cancellationToken = default);
}

public record AdminMailboxGatewayConnection(
    string Username,
    string Password,
    string SenderEmail,
    string SenderDisplayName,
    string SmtpHost,
    int SmtpPort,
    bool SmtpUseSsl,
    string Pop3Host,
    int Pop3Port,
    bool Pop3UseSsl);

public record AdminMailboxGatewaySendRequest(
    AdminMailboxGatewayConnection Connection,
    string To,
    string Subject,
    string Body,
    bool IsHtml,
    IReadOnlyList<AdminMailboxGatewayAttachment>? Attachments = null);

public record AdminMailboxGatewayAttachment(
    string FileName,
    string? ContentType,
    byte[] ContentBytes);

public record AdminMailboxGatewayFetchRequest(
    AdminMailboxGatewayConnection Connection,
    int Take);

public record AdminMailboxGatewayInboundMessage(
    string ExternalMessageId,
    string Subject,
    string FromAddress,
    string ToAddress,
    string BodyText,
    string? BodyHtml,
    DateTime OccurredAtUtc,
    IReadOnlyList<AdminMailboxGatewayInboundAttachment>? Attachments = null);

public record AdminMailboxGatewayInboundAttachment(
    string FileName,
    string? ContentType,
    byte[] ContentBytes);
