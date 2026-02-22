namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminMailboxStore
{
    Task<AdminMailboxStoreSnapshot> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AdminMailboxStoreSnapshot snapshot, CancellationToken cancellationToken = default);
}

public record AdminMailboxStoreSnapshot(
    AdminMailboxStoreSettings? Settings,
    IReadOnlyList<AdminMailboxStoredMessage> Messages,
    AdminMailboxStoreSyncState SyncState)
{
    public static AdminMailboxStoreSnapshot Empty { get; } = new(
        Settings: null,
        Messages: Array.Empty<AdminMailboxStoredMessage>(),
        SyncState: new AdminMailboxStoreSyncState(null, null, null));
}

public record AdminMailboxStoreSettings(
    bool Enabled,
    string SenderDisplayName,
    string SenderEmail,
    string Username,
    string Password,
    string SmtpHost,
    int SmtpPort,
    bool SmtpUseSsl,
    string Pop3Host,
    int Pop3Port,
    bool Pop3UseSsl,
    int SyncWindowSize,
    int PollIntervalSeconds,
    DateTime UpdatedAtUtc);

public record AdminMailboxStoredMessage(
    string Id,
    string Direction,
    string Subject,
    string FromAddress,
    string ToAddress,
    string Preview,
    string BodyText,
    string? BodyHtml,
    IReadOnlyList<AdminMailboxStoredAttachment> Attachments,
    DateTime OccurredAtUtc,
    bool IsRead,
    string? ExternalMessageId,
    DateTime StoredAtUtc);

public record AdminMailboxStoredAttachment(
    string Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentBase64,
    bool IsImage);

public record AdminMailboxStoreSyncState(
    DateTime? LastSyncAtUtc,
    string? LastSyncStatus,
    string? LastSyncError);
