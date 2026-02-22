using System.Net.Mail;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminMailboxService : IAdminMailboxService
{
    private const string AuditTargetType = "AdminMailbox";
    private const int MaxStoredMessages = 400;
    private const int MinPort = 1;
    private const int MaxPort = 65535;
    private const int MinSyncWindow = 10;
    private const int MaxSyncWindow = 200;
    private const int MinPollIntervalSeconds = 30;
    private const int MaxPollIntervalSeconds = 1800;
    private const int MaxSubjectLength = 220;
    private const int MaxBodyLength = 100_000;
    private const int MaxAttachmentCount = 10;
    private const int MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    private const int MaxTotalAttachmentsSizeBytes = 25 * 1024 * 1024;
    private const int MaxAttachmentFileNameLength = 180;

    private readonly IAdminMailboxStore _store;
    private readonly IAdminMailboxGateway _gateway;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminMailboxService> _logger;

    public AdminMailboxService(
        IAdminMailboxStore store,
        IAdminMailboxGateway gateway,
        IUserRepository userRepository,
        INotificationService notificationService,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminMailboxService>? logger = null)
    {
        _store = store;
        _gateway = gateway;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminMailboxService>.Instance;
    }

    public async Task<AdminMailboxSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken);
        return MapSettings(snapshot);
    }

    public async Task<AdminMailboxOperationResultDto> UpsertSettingsAsync(
        AdminMailboxUpsertSettingsRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken);
        var existing = snapshot.Settings;

        var senderDisplayName = NormalizeText(request.SenderDisplayName, 120) ?? "ConsertaPraMim";
        var senderEmail = NormalizeEmail(request.SenderEmail);
        var username = NormalizeEmail(request.Username);
        var password = string.IsNullOrWhiteSpace(request.Password)
            ? existing?.Password
            : request.Password.Trim();
        var smtpHost = NormalizeHost(request.SmtpHost);
        var pop3Host = NormalizeHost(request.Pop3Host);

        if (string.IsNullOrWhiteSpace(username))
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_username_required",
                ErrorMessage: "Usuario SMTP/POP3 obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_password_required",
                ErrorMessage: "Senha/App Password obrigatoria.");
        }

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            senderEmail = username;
        }

        if (string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(smtpHost) ||
            string.IsNullOrWhiteSpace(pop3Host))
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_invalid_settings",
                ErrorMessage: "Preencha sender email, SMTP host e POP3 host.");
        }

        var normalized = new AdminMailboxStoreSettings(
            Enabled: request.Enabled,
            SenderDisplayName: senderDisplayName,
            SenderEmail: senderEmail,
            Username: username,
            Password: password,
            SmtpHost: smtpHost,
            SmtpPort: Math.Clamp(request.SmtpPort <= 0 ? 587 : request.SmtpPort, MinPort, MaxPort),
            SmtpUseSsl: request.SmtpUseSsl,
            Pop3Host: pop3Host,
            Pop3Port: Math.Clamp(request.Pop3Port <= 0 ? 995 : request.Pop3Port, MinPort, MaxPort),
            Pop3UseSsl: request.Pop3UseSsl,
            SyncWindowSize: Math.Clamp(request.SyncWindowSize <= 0 ? 40 : request.SyncWindowSize, MinSyncWindow, MaxSyncWindow),
            PollIntervalSeconds: Math.Clamp(request.PollIntervalSeconds <= 0 ? 120 : request.PollIntervalSeconds, MinPollIntervalSeconds, MaxPollIntervalSeconds),
            UpdatedAtUtc: DateTime.UtcNow);

        await _store.SaveAsync(
            snapshot with
            {
                Settings = normalized
            },
            cancellationToken);

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = NormalizeText(actorEmail, 320) ?? "admin@unknown",
            Action = "AdminMailboxSettingsUpdated",
            TargetType = AuditTargetType,
            TargetId = Guid.Empty,
            Metadata = JsonSerializer.Serialize(new
            {
                normalized.Enabled,
                normalized.SenderDisplayName,
                normalized.SenderEmail,
                normalized.Username,
                normalized.SmtpHost,
                normalized.SmtpPort,
                normalized.SmtpUseSsl,
                normalized.Pop3Host,
                normalized.Pop3Port,
                normalized.Pop3UseSsl,
                normalized.SyncWindowSize,
                normalized.PollIntervalSeconds
            })
        });

        return new AdminMailboxOperationResultDto(true);
    }

    public async Task<AdminMailboxListResponseDto> GetMessagesAsync(
        AdminMailboxListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken);
        var safePage = query.Page < 1 ? 1 : query.Page;
        var safePageSize = query.PageSize <= 0 ? 30 : Math.Min(query.PageSize, 200);
        var normalizedFolder = NormalizeFolder(query.Folder);
        var normalizedSearch = NormalizeText(query.Search, 200);

        IEnumerable<AdminMailboxStoredMessage> filtered = snapshot.Messages
            .OrderByDescending(message => message.OccurredAtUtc);

        filtered = normalizedFolder switch
        {
            "inbox" => filtered.Where(message => message.Direction.Equals("inbound", StringComparison.OrdinalIgnoreCase)),
            "sent" => filtered.Where(message => message.Direction.Equals("outbound", StringComparison.OrdinalIgnoreCase)),
            _ => filtered
        };

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filtered = filtered.Where(message =>
                message.Subject.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                message.FromAddress.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                message.ToAddress.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                message.Preview.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = filtered.Count();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)safePageSize);
        var items = filtered
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(MapSummary)
            .ToList();

        return new AdminMailboxListResponseDto(
            Items: items,
            Page: safePage,
            PageSize: safePageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            LastSyncAtUtc: snapshot.SyncState.LastSyncAtUtc,
            LastSyncStatus: snapshot.SyncState.LastSyncStatus,
            LastSyncError: snapshot.SyncState.LastSyncError);
    }

    public async Task<AdminMailboxOperationResultDto> GetMessageByIdAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessageId = NormalizeText(messageId, 200);
        if (string.IsNullOrWhiteSpace(normalizedMessageId))
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_message_id_required",
                ErrorMessage: "MessageId obrigatorio.");
        }

        var snapshot = await _store.LoadAsync(cancellationToken);
        var message = snapshot.Messages.FirstOrDefault(item =>
            item.Id.Equals(normalizedMessageId, StringComparison.OrdinalIgnoreCase));

        if (message == null)
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_message_not_found",
                ErrorMessage: "Email nao encontrado.");
        }

        if (message.Direction.Equals("inbound", StringComparison.OrdinalIgnoreCase) && !message.IsRead)
        {
            var updated = snapshot.Messages
                .Select(item => item.Id.Equals(message.Id, StringComparison.OrdinalIgnoreCase)
                    ? item with { IsRead = true }
                    : item)
                .ToList();

            message = message with { IsRead = true };
            await _store.SaveAsync(snapshot with { Messages = updated }, cancellationToken);
        }

        return new AdminMailboxOperationResultDto(true, Message: MapDetails(message));
    }

    public async Task<AdminMailboxOperationResultDto> MarkMessageReadAsync(
        string messageId,
        bool isRead,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessageId = NormalizeText(messageId, 200);
        if (string.IsNullOrWhiteSpace(normalizedMessageId))
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_message_id_required",
                ErrorMessage: "MessageId obrigatorio.");
        }

        var snapshot = await _store.LoadAsync(cancellationToken);
        var targetIndex = snapshot.Messages
            .ToList()
            .FindIndex(item => item.Id.Equals(normalizedMessageId, StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0)
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_message_not_found",
                ErrorMessage: "Email nao encontrado.");
        }

        var updated = snapshot.Messages.ToList();
        updated[targetIndex] = updated[targetIndex] with { IsRead = isRead };
        await _store.SaveAsync(snapshot with { Messages = updated }, cancellationToken);

        return new AdminMailboxOperationResultDto(true, Message: MapDetails(updated[targetIndex]));
    }

    public async Task<AdminMailboxOperationResultDto> SendAsync(
        AdminMailboxSendRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        var to = NormalizeEmail(request.To);
        var subject = NormalizeText(request.Subject, MaxSubjectLength);
        var body = request.Body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_send_invalid_payload",
                ErrorMessage: "Preencha destinatario, assunto e corpo do email.");
        }

        if (body.Length > MaxBodyLength)
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_send_body_too_large",
                ErrorMessage: $"Corpo do email excede limite de {MaxBodyLength} caracteres.");
        }

        var allowedRecipients = (await _userRepository.GetAllAsync())
            .Where(user => user.IsActive)
            .Where(user => user.Role is UserRole.Client or UserRole.Provider)
            .Select(user => NormalizeEmail(user.Email))
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(to) || !allowedRecipients.Contains(to))
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_send_recipient_not_allowed",
                ErrorMessage: "Envio permitido apenas para clientes e prestadores ativos.");
        }

        var requestedAttachments = request.Attachments ?? Array.Empty<AdminMailboxAttachmentDto>();
        if (requestedAttachments.Count > MaxAttachmentCount)
        {
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_send_too_many_attachments",
                ErrorMessage: $"Quantidade de anexos excede o limite de {MaxAttachmentCount}.");
        }

        var gatewayAttachments = new List<AdminMailboxGatewayAttachment>(requestedAttachments.Count);
        long totalAttachmentsSize = 0;
        foreach (var attachment in requestedAttachments)
        {
            var fileName = NormalizeText(attachment.FileName, MaxAttachmentFileNameLength);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return new AdminMailboxOperationResultDto(
                    false,
                    ErrorCode: "admin_mailbox_send_attachment_invalid_name",
                    ErrorMessage: "Um dos anexos nao possui nome de arquivo valido.");
            }

            if (string.IsNullOrWhiteSpace(attachment.ContentBase64))
            {
                return new AdminMailboxOperationResultDto(
                    false,
                    ErrorCode: "admin_mailbox_send_attachment_invalid_content",
                    ErrorMessage: $"Anexo '{fileName}' sem conteudo.");
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(attachment.ContentBase64.Trim());
            }
            catch (FormatException)
            {
                return new AdminMailboxOperationResultDto(
                    false,
                    ErrorCode: "admin_mailbox_send_attachment_invalid_content",
                    ErrorMessage: $"Anexo '{fileName}' com conteudo invalido.");
            }

            if (bytes.Length == 0)
            {
                return new AdminMailboxOperationResultDto(
                    false,
                    ErrorCode: "admin_mailbox_send_attachment_empty",
                    ErrorMessage: $"Anexo '{fileName}' vazio.");
            }

            if (bytes.Length > MaxAttachmentSizeBytes)
            {
                return new AdminMailboxOperationResultDto(
                    false,
                    ErrorCode: "admin_mailbox_send_attachment_too_large",
                    ErrorMessage: $"Anexo '{fileName}' excede o limite de {MaxAttachmentSizeBytes / (1024 * 1024)} MB.");
            }

            totalAttachmentsSize += bytes.Length;
            if (totalAttachmentsSize > MaxTotalAttachmentsSizeBytes)
            {
                return new AdminMailboxOperationResultDto(
                    false,
                    ErrorCode: "admin_mailbox_send_attachments_too_large",
                    ErrorMessage: $"Tamanho total dos anexos excede {MaxTotalAttachmentsSizeBytes / (1024 * 1024)} MB.");
            }

            gatewayAttachments.Add(new AdminMailboxGatewayAttachment(
                FileName: fileName,
                ContentType: NormalizeText(attachment.ContentType, 120),
                ContentBytes: bytes));
        }

        var snapshot = await _store.LoadAsync(cancellationToken);
        if (!TryBuildConnection(snapshot.Settings, out var connection, out var errorCode, out var errorMessage))
        {
            return new AdminMailboxOperationResultDto(false, errorCode, errorMessage);
        }

        try
        {
            await _gateway.SendAsync(
                new AdminMailboxGatewaySendRequest(
                    Connection: connection,
                    To: to!,
                    Subject: subject!,
                    Body: body,
                    IsHtml: request.IsHtml,
                    Attachments: gatewayAttachments),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar email admin mailbox para {To}.", to);
            return new AdminMailboxOperationResultDto(
                false,
                ErrorCode: "admin_mailbox_send_failed",
                ErrorMessage: $"Falha ao enviar email: {ex.Message}");
        }

        var now = DateTime.UtcNow;
        var outboundAttachments = gatewayAttachments
            .Select(attachment => new AdminMailboxStoredAttachment(
                Id: $"att-{Guid.NewGuid():N}",
                FileName: attachment.FileName,
                ContentType: string.IsNullOrWhiteSpace(attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType!,
                SizeBytes: attachment.ContentBytes.LongLength,
                ContentBase64: Convert.ToBase64String(attachment.ContentBytes),
                IsImage: IsImageContentType(attachment.ContentType)))
            .ToList();

        var outbound = new AdminMailboxStoredMessage(
            Id: $"out-{Guid.NewGuid():N}",
            Direction: "outbound",
            Subject: subject!,
            FromAddress: connection.SenderEmail,
            ToAddress: to!,
            Preview: BuildPreview(body, request.IsHtml) ?? string.Empty,
            BodyText: request.IsHtml ? BuildPreview(body, false, 5000) ?? string.Empty : body,
            BodyHtml: request.IsHtml ? body : null,
            Attachments: outboundAttachments,
            OccurredAtUtc: now,
            IsRead: true,
            ExternalMessageId: null,
            StoredAtUtc: now);

        var merged = MergeAndTrimMessages(snapshot.Messages, new[] { outbound });
        await _store.SaveAsync(snapshot with { Messages = merged }, cancellationToken);

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = NormalizeText(actorEmail, 320) ?? "admin@unknown",
            Action = "AdminMailboxEmailSent",
            TargetType = AuditTargetType,
            TargetId = Guid.Empty,
            Metadata = JsonSerializer.Serialize(new
            {
                to = to!,
                subject = subject!,
                isHtml = request.IsHtml,
                attachmentCount = gatewayAttachments.Count
            })
        });

        return new AdminMailboxOperationResultDto(true, Message: MapDetails(outbound));
    }

    public async Task<AdminMailboxSyncResultDto> SyncInboxAsync(
        bool notifyAdmins,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken);
        if (!TryBuildConnection(snapshot.Settings, out var connection, out var errorCode, out var errorMessage))
        {
            return new AdminMailboxSyncResultDto(
                false,
                ErrorCode: errorCode,
                ErrorMessage: errorMessage);
        }

        IReadOnlyList<AdminMailboxGatewayInboundMessage> fetched;
        try
        {
            fetched = await _gateway.FetchInboundAsync(
                new AdminMailboxGatewayFetchRequest(
                    Connection: connection,
                    Take: Math.Clamp(snapshot.Settings!.SyncWindowSize, MinSyncWindow, MaxSyncWindow)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao sincronizar inbox admin mailbox.");
            var failedSnapshot = snapshot with
            {
                SyncState = new AdminMailboxStoreSyncState(
                    LastSyncAtUtc: DateTime.UtcNow,
                    LastSyncStatus: "failed",
                    LastSyncError: Truncate(ex.Message, 500))
            };
            await _store.SaveAsync(failedSnapshot, cancellationToken);
            return new AdminMailboxSyncResultDto(
                false,
                ErrorCode: "admin_mailbox_sync_failed",
                ErrorMessage: ex.Message);
        }

        var existingMessages = snapshot.Messages.ToList();
        var existingInboundCount = existingMessages.Count(message =>
            message.Direction.Equals("inbound", StringComparison.OrdinalIgnoreCase));

        var existingInboundExternalIds = new HashSet<string>(
            existingMessages
                .Where(message => message.Direction.Equals("inbound", StringComparison.OrdinalIgnoreCase))
                .Select(message => NormalizeText(message.ExternalMessageId, 320))
                .Where(value => !string.IsNullOrWhiteSpace(value))!,
            StringComparer.OrdinalIgnoreCase);

        var newInboundMessages = new List<AdminMailboxStoredMessage>();
        foreach (var inbound in fetched.OrderByDescending(item => item.OccurredAtUtc))
        {
            var externalId = NormalizeText(inbound.ExternalMessageId, 320);
            if (string.IsNullOrWhiteSpace(externalId))
            {
                externalId = BuildFallbackExternalId(inbound);
            }

            if (!existingInboundExternalIds.Add(externalId))
            {
                continue;
            }

            var bodyText = NormalizeText(inbound.BodyText, MaxBodyLength) ?? string.Empty;
            var bodyHtml = NormalizeText(inbound.BodyHtml, MaxBodyLength);
            var preview = BuildPreview(bodyText, false) ?? BuildPreview(bodyHtml, true) ?? string.Empty;
            var inboundAttachments = NormalizeInboundAttachments(inbound.Attachments);

            var stored = new AdminMailboxStoredMessage(
                Id: $"in-{Guid.NewGuid():N}",
                Direction: "inbound",
                Subject: NormalizeText(inbound.Subject, MaxSubjectLength) ?? "(sem assunto)",
                FromAddress: NormalizeText(inbound.FromAddress, 320) ?? "desconhecido",
                ToAddress: NormalizeText(inbound.ToAddress, 320) ?? connection.SenderEmail,
                Preview: preview,
                BodyText: bodyText,
                BodyHtml: bodyHtml,
                Attachments: inboundAttachments,
                OccurredAtUtc: inbound.OccurredAtUtc,
                IsRead: false,
                ExternalMessageId: externalId,
                StoredAtUtc: DateTime.UtcNow);

            newInboundMessages.Add(stored);
        }

        var merged = MergeAndTrimMessages(existingMessages, newInboundMessages);
        var updatedSnapshot = snapshot with
        {
            Messages = merged,
            SyncState = new AdminMailboxStoreSyncState(
                LastSyncAtUtc: DateTime.UtcNow,
                LastSyncStatus: "success",
                LastSyncError: null)
        };
        await _store.SaveAsync(updatedSnapshot, cancellationToken);

        var shouldNotify = notifyAdmins && existingInboundCount > 0 && newInboundMessages.Count > 0;
        if (shouldNotify)
        {
            await NotifyAdminsForInboundMessagesAsync(newInboundMessages, cancellationToken);
        }

        return new AdminMailboxSyncResultDto(
            Success: true,
            FetchedCount: fetched.Count,
            NewMessagesCount: newInboundMessages.Count,
            SyncedAtUtc: updatedSnapshot.SyncState.LastSyncAtUtc);
    }

    public async Task<IReadOnlyList<AdminMailboxRecipientDto>> GetRecipientsAsync(
        string? role,
        string? search,
        int take,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRole = NormalizeText(role, 20)?.ToLowerInvariant();
        var normalizedSearch = NormalizeText(search, 120);
        var safeTake = take <= 0 ? 50 : Math.Min(take, 300);
        var users = await _userRepository.GetAllAsync();

        var filtered = users
            .Where(user => user.IsActive)
            .Where(user =>
                normalizedRole == null ||
                (normalizedRole == "client" && user.Role == UserRole.Client) ||
                (normalizedRole == "provider" && user.Role == UserRole.Provider) ||
                (normalizedRole == "admin" && user.Role == UserRole.Admin))
            .Where(user =>
                string.IsNullOrWhiteSpace(normalizedSearch) ||
                (!string.IsNullOrWhiteSpace(user.Name) &&
                 user.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(user.Email) &&
                 user.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Email)
            .Take(safeTake)
            .Select(user => new AdminMailboxRecipientDto(
                UserId: user.Id,
                Name: NormalizeText(user.Name, 120) ?? "Usuario",
                Email: NormalizeText(user.Email, 320) ?? string.Empty,
                Role: user.Role.ToString().ToLowerInvariant(),
                IsActive: user.IsActive))
            .ToList();

        return filtered;
    }

    private async Task NotifyAdminsForInboundMessagesAsync(
        IReadOnlyList<AdminMailboxStoredMessage> newMessages,
        CancellationToken cancellationToken)
    {
        var admins = (await _userRepository.GetAllAsync())
            .Where(user => user.Role == UserRole.Admin && user.IsActive)
            .Select(user => user.Id)
            .Distinct()
            .ToList();

        if (admins.Count == 0)
        {
            return;
        }

        var topMessage = newMessages
            .OrderByDescending(message => message.OccurredAtUtc)
            .First();
        var title = newMessages.Count == 1
            ? "Novo email recebido"
            : $"Novos emails recebidos ({newMessages.Count})";
        var message = $"{topMessage.FromAddress}: {topMessage.Subject}";

        foreach (var adminId in admins)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _notificationService.SendNotificationAsync(
                    recipient: adminId.ToString("N"),
                    subject: title,
                    message: message,
                    actionUrl: "/AdminMailbox/Index",
                    data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "admin_event_inbound_email",
                        ["messageId"] = topMessage.Id
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao notificar admin {AdminUserId} sobre novo email inbound.", adminId);
            }
        }
    }

    private static IReadOnlyList<AdminMailboxStoredMessage> MergeAndTrimMessages(
        IReadOnlyList<AdminMailboxStoredMessage> existing,
        IReadOnlyList<AdminMailboxStoredMessage> incoming)
    {
        var merged = incoming
            .Concat(existing)
            .GroupBy(message => message.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(message => message.StoredAtUtc)
                .First())
            .OrderByDescending(message => message.OccurredAtUtc)
            .ThenByDescending(message => message.StoredAtUtc)
            .Take(MaxStoredMessages)
            .ToList();
        return merged;
    }

    private static IReadOnlyList<AdminMailboxStoredAttachment> NormalizeInboundAttachments(
        IReadOnlyList<AdminMailboxGatewayInboundAttachment>? attachments)
    {
        if (attachments is not { Count: > 0 })
        {
            return Array.Empty<AdminMailboxStoredAttachment>();
        }

        var normalized = new List<AdminMailboxStoredAttachment>();
        long totalBytes = 0;
        foreach (var attachment in attachments.Take(MaxAttachmentCount))
        {
            var fileName = NormalizeText(attachment.FileName, MaxAttachmentFileNameLength);
            if (string.IsNullOrWhiteSpace(fileName) || attachment.ContentBytes is not { Length: > 0 })
            {
                continue;
            }

            if (attachment.ContentBytes.LongLength > MaxAttachmentSizeBytes)
            {
                continue;
            }

            totalBytes += attachment.ContentBytes.LongLength;
            if (totalBytes > MaxTotalAttachmentsSizeBytes)
            {
                break;
            }

            var contentType = NormalizeText(attachment.ContentType, 120);
            if (string.IsNullOrWhiteSpace(contentType))
            {
                contentType = "application/octet-stream";
            }

            normalized.Add(new AdminMailboxStoredAttachment(
                Id: $"att-{Guid.NewGuid():N}",
                FileName: fileName,
                ContentType: contentType,
                SizeBytes: attachment.ContentBytes.LongLength,
                ContentBase64: Convert.ToBase64String(attachment.ContentBytes),
                IsImage: IsImageContentType(contentType)));
        }

        return normalized;
    }

    private static bool IsImageContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Trim().StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFallbackExternalId(AdminMailboxGatewayInboundMessage inbound)
    {
        var raw = $"{inbound.FromAddress}|{inbound.Subject}|{inbound.OccurredAtUtc:O}";
        return $"fallback-{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw))}";
    }

    private static AdminMailboxMessageSummaryDto MapSummary(AdminMailboxStoredMessage message)
    {
        return new AdminMailboxMessageSummaryDto(
            Id: message.Id,
            Direction: message.Direction,
            Subject: message.Subject,
            FromAddress: message.FromAddress,
            ToAddress: message.ToAddress,
            Preview: message.Preview,
            OccurredAtUtc: message.OccurredAtUtc,
            IsRead: message.IsRead,
            ExternalMessageId: message.ExternalMessageId,
            AttachmentsCount: message.Attachments?.Count ?? 0);
    }

    private static AdminMailboxMessageDetailsDto MapDetails(AdminMailboxStoredMessage message)
    {
        return new AdminMailboxMessageDetailsDto(
            Id: message.Id,
            Direction: message.Direction,
            Subject: message.Subject,
            FromAddress: message.FromAddress,
            ToAddress: message.ToAddress,
            Preview: message.Preview,
            BodyText: message.BodyText,
            BodyHtml: message.BodyHtml,
            Attachments: (message.Attachments ?? Array.Empty<AdminMailboxStoredAttachment>())
                .Select(attachment => new AdminMailboxMessageAttachmentDto(
                    Id: attachment.Id,
                    FileName: attachment.FileName,
                    ContentType: attachment.ContentType,
                    SizeBytes: attachment.SizeBytes,
                    ContentBase64: attachment.ContentBase64,
                    IsImage: attachment.IsImage))
                .ToList(),
            OccurredAtUtc: message.OccurredAtUtc,
            IsRead: message.IsRead,
            ExternalMessageId: message.ExternalMessageId);
    }

    private static AdminMailboxSettingsDto MapSettings(AdminMailboxStoreSnapshot snapshot)
    {
        var settings = snapshot.Settings;
        if (settings == null)
        {
            return new AdminMailboxSettingsDto(
                IsConfigured: false,
                Enabled: false,
                SenderDisplayName: "ConsertaPraMim",
                SenderEmail: string.Empty,
                Username: string.Empty,
                HasPassword: false,
                SmtpHost: "smtp.gmail.com",
                SmtpPort: 587,
                SmtpUseSsl: true,
                Pop3Host: "pop.gmail.com",
                Pop3Port: 995,
                Pop3UseSsl: true,
                SyncWindowSize: 40,
                PollIntervalSeconds: 120,
                LastSyncAtUtc: snapshot.SyncState.LastSyncAtUtc,
                LastSyncStatus: snapshot.SyncState.LastSyncStatus,
                LastSyncError: snapshot.SyncState.LastSyncError);
        }

        var isConfigured = HasRequiredConnection(settings);
        return new AdminMailboxSettingsDto(
            IsConfigured: isConfigured,
            Enabled: settings.Enabled,
            SenderDisplayName: settings.SenderDisplayName,
            SenderEmail: settings.SenderEmail,
            Username: settings.Username,
            HasPassword: !string.IsNullOrWhiteSpace(settings.Password),
            SmtpHost: settings.SmtpHost,
            SmtpPort: settings.SmtpPort,
            SmtpUseSsl: settings.SmtpUseSsl,
            Pop3Host: settings.Pop3Host,
            Pop3Port: settings.Pop3Port,
            Pop3UseSsl: settings.Pop3UseSsl,
            SyncWindowSize: settings.SyncWindowSize,
            PollIntervalSeconds: settings.PollIntervalSeconds,
            LastSyncAtUtc: snapshot.SyncState.LastSyncAtUtc,
            LastSyncStatus: snapshot.SyncState.LastSyncStatus,
            LastSyncError: snapshot.SyncState.LastSyncError);
    }

    private static bool TryBuildConnection(
        AdminMailboxStoreSettings? settings,
        out AdminMailboxGatewayConnection connection,
        out string errorCode,
        out string errorMessage)
    {
        if (settings == null || !HasRequiredConnection(settings))
        {
            connection = new AdminMailboxGatewayConnection(
                Username: string.Empty,
                Password: string.Empty,
                SenderEmail: string.Empty,
                SenderDisplayName: string.Empty,
                SmtpHost: string.Empty,
                SmtpPort: 587,
                SmtpUseSsl: true,
                Pop3Host: string.Empty,
                Pop3Port: 995,
                Pop3UseSsl: true);
            errorCode = "admin_mailbox_not_configured";
            errorMessage = "Mailbox nao configurado. Preencha SMTP/POP3 nas configuracoes.";
            return false;
        }

        if (!settings.Enabled)
        {
            connection = new AdminMailboxGatewayConnection(
                Username: string.Empty,
                Password: string.Empty,
                SenderEmail: string.Empty,
                SenderDisplayName: string.Empty,
                SmtpHost: string.Empty,
                SmtpPort: 587,
                SmtpUseSsl: true,
                Pop3Host: string.Empty,
                Pop3Port: 995,
                Pop3UseSsl: true);
            errorCode = "admin_mailbox_disabled";
            errorMessage = "Mailbox admin esta desabilitado.";
            return false;
        }

        connection = new AdminMailboxGatewayConnection(
            Username: settings.Username,
            Password: settings.Password,
            SenderEmail: settings.SenderEmail,
            SenderDisplayName: settings.SenderDisplayName,
            SmtpHost: settings.SmtpHost,
            SmtpPort: settings.SmtpPort,
            SmtpUseSsl: settings.SmtpUseSsl,
            Pop3Host: settings.Pop3Host,
            Pop3Port: settings.Pop3Port,
            Pop3UseSsl: settings.Pop3UseSsl);
        errorCode = string.Empty;
        errorMessage = string.Empty;
        return true;
    }

    private static bool HasRequiredConnection(AdminMailboxStoreSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.Username) &&
               !string.IsNullOrWhiteSpace(settings.Password) &&
               !string.IsNullOrWhiteSpace(settings.SenderEmail) &&
               !string.IsNullOrWhiteSpace(settings.SmtpHost) &&
               !string.IsNullOrWhiteSpace(settings.Pop3Host);
    }

    private static string NormalizeFolder(string? folder)
    {
        var normalized = NormalizeText(folder, 20)?.ToLowerInvariant();
        return normalized switch
        {
            "inbox" => "inbox",
            "sent" => "sent",
            "all" => "all",
            _ => "inbox"
        };
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }

    private static string Truncate(string? value, int maxLength)
    {
        return NormalizeText(value, maxLength) ?? string.Empty;
    }

    private static string? NormalizeHost(string? host)
    {
        var normalized = NormalizeText(host, 255);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.ToLowerInvariant();
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalized = NormalizeText(email, 320);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        try
        {
            return new MailAddress(normalized).Address;
        }
        catch
        {
            return null;
        }
    }

    private static string? BuildPreview(string? body, bool isHtml, int maxLength = 180)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var normalized = body
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();
        if (isHtml)
        {
            normalized = normalized
                .Replace("<br>", " ", StringComparison.OrdinalIgnoreCase)
                .Replace("<br/>", " ", StringComparison.OrdinalIgnoreCase)
                .Replace("<br />", " ", StringComparison.OrdinalIgnoreCase);
        }

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return $"{normalized[..Math.Max(1, maxLength - 3)]}...";
    }
}
