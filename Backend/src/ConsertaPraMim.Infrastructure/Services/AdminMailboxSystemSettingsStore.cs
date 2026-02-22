using System.Text.Json;
using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class AdminMailboxSystemSettingsStore : IAdminMailboxStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConsertaPraMimDbContext _dbContext;
    private readonly ILogger<AdminMailboxSystemSettingsStore> _logger;

    public AdminMailboxSystemSettingsStore(
        ConsertaPraMimDbContext dbContext,
        ILogger<AdminMailboxSystemSettingsStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminMailboxStoreSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Key == SystemSettingKeys.AdminMailboxSnapshotV1,
                cancellationToken);

        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return AdminMailboxStoreSnapshot.Empty;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<AdminMailboxStoreSnapshot>(setting.Value, JsonOptions);
            if (snapshot == null)
            {
                return AdminMailboxStoreSnapshot.Empty;
            }

            return snapshot with
            {
                Messages = snapshot.Messages ?? Array.Empty<AdminMailboxStoredMessage>(),
                SyncState = snapshot.SyncState ?? new AdminMailboxStoreSyncState(null, null, null)
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Snapshot de mailbox admin invalido em SystemSettings (key={Key}).",
                SystemSettingKeys.AdminMailboxSnapshotV1);
            return AdminMailboxStoreSnapshot.Empty;
        }
    }

    public async Task SaveAsync(AdminMailboxStoreSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var normalized = snapshot with
        {
            Messages = snapshot.Messages ?? Array.Empty<AdminMailboxStoredMessage>(),
            SyncState = snapshot.SyncState ?? new AdminMailboxStoreSyncState(null, null, null)
        };

        var serialized = JsonSerializer.Serialize(normalized, JsonOptions);
        var existing = await _dbContext.SystemSettings
            .SingleOrDefaultAsync(
                x => x.Key == SystemSettingKeys.AdminMailboxSnapshotV1,
                cancellationToken);

        if (existing == null)
        {
            _dbContext.SystemSettings.Add(new SystemSetting
            {
                Key = SystemSettingKeys.AdminMailboxSnapshotV1,
                Value = serialized,
                Description = "Snapshot persistido do mailbox admin (SMTP/POP3, inbox/sent e estado de sync)."
            });
        }
        else
        {
            existing.Value = serialized;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
