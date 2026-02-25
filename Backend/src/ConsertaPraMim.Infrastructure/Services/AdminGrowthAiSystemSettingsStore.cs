using System.Text.Json;
using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class AdminGrowthAiSystemSettingsStore : IAdminGrowthAiStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConsertaPraMimDbContext _dbContext;
    private readonly ILogger<AdminGrowthAiSystemSettingsStore> _logger;

    public AdminGrowthAiSystemSettingsStore(
        ConsertaPraMimDbContext dbContext,
        ILogger<AdminGrowthAiSystemSettingsStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminGrowthAiStoreSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Key == SystemSettingKeys.AdminGrowthAiSnapshotV1,
                cancellationToken);

        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return AdminGrowthAiStoreSnapshot.Empty;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<AdminGrowthAiStoreSnapshot>(setting.Value, JsonOptions);
            if (snapshot == null)
            {
                return AdminGrowthAiStoreSnapshot.Empty;
            }

            return snapshot with
            {
                Analyses = snapshot.Analyses ?? Array.Empty<AdminGrowthAiAnalysisDto>()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Snapshot de growth AI invalido em SystemSettings (key={Key}).",
                SystemSettingKeys.AdminGrowthAiSnapshotV1);
            return AdminGrowthAiStoreSnapshot.Empty;
        }
    }

    public async Task SaveAsync(AdminGrowthAiStoreSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var normalized = snapshot with
        {
            Analyses = snapshot.Analyses ?? Array.Empty<AdminGrowthAiAnalysisDto>()
        };

        var serialized = JsonSerializer.Serialize(normalized, JsonOptions);
        var existing = await _dbContext.SystemSettings
            .SingleOrDefaultAsync(
                item => item.Key == SystemSettingKeys.AdminGrowthAiSnapshotV1,
                cancellationToken);

        if (existing == null)
        {
            _dbContext.SystemSettings.Add(new SystemSetting
            {
                Key = SystemSettingKeys.AdminGrowthAiSnapshotV1,
                Value = serialized,
                Description = "Snapshot do copiloto IA de growth/liquidez (configuracoes e historico de analises)."
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
