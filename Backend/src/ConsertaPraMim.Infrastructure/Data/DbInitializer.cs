using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Text.Json;

namespace ConsertaPraMim.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
        var configuration = scope.ServiceProvider.GetService<IConfiguration>();
        var hostEnvironment = scope.ServiceProvider.GetService<IHostEnvironment>();
        var seedEnabled = configuration?.GetValue<bool?>("Seed:Enabled")
            ?? hostEnvironment?.IsDevelopment() == true;
        var shouldResetDatabase = configuration?.GetValue<bool?>("Seed:Reset") ?? false;
        var shouldSeedDefaultAdmin = configuration?.GetValue<bool?>("Seed:CreateDefaultAdmin")
            ?? hostEnvironment?.IsDevelopment() == true;
        var defaultSeedPassword = configuration?["Seed:DefaultPassword"] ?? "SeedDev!2026";

        var executionStrategy = context.Database.CreateExecutionStrategy();

        // Apply migrations if any using SQL resiliency strategy.
        await executionStrategy.ExecuteAsync(async () =>
        {
            await context.Database.MigrateAsync();
        });

        // Always ensure runtime system settings exist, even when data seed is disabled.
        await EnsureSystemSettingsDefaultsAsync(context, configuration);

        if (!seedEnabled)
        {
            return;
        }

        if (!IsStrongSeedPassword(defaultSeedPassword))
        {
            throw new InvalidOperationException("Seed:DefaultPassword invalida. Use ao menos 8 caracteres com maiuscula, minuscula, numero e caractere especial.");
        }

        if (shouldResetDatabase)
        {
            if (hostEnvironment?.IsDevelopment() != true)
            {
                throw new InvalidOperationException("Seed:Reset so pode ser usado em Development.");
            }

            // Clean all data without dropping the database (works without DROP DATABASE permission).
            await ClearDatabaseAsync(context);
        }

        await EnsureServiceCategoriesAsync(context);
        await EnsureChecklistTemplateDefaultsAsync(context);
        await EnsurePlanGovernanceDefaultsAsync(context);
        await EnsureNoShowRiskPolicyDefaultsAsync(context);
        await EnsureNoShowAlertThresholdDefaultsAsync(context);
        await EnsureServiceFinancialPolicyRuleDefaultsAsync(context);
        await EnsureProviderCreditWalletsAsync(context);
        await EnsureSystemSettingsDefaultsAsync(context, configuration);
        await EnsureApiMonitoringSeedAsync(context);

        if (!shouldResetDatabase && await context.Users.AnyAsync())
        {
            // Preserve existing data when reset is disabled.
            return;
        }

        // Seed Providers (20) with random distribution across Bronze/Silver/Gold plans
        var providers = BuildSeedProviders(defaultSeedPassword);

        // Seed Clients (5)
        var clients = new List<User>
        {
            new User { Id = Guid.NewGuid(), Name = "Cliente 01", Email = "cliente1@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultSeedPassword), Phone = "21911110001", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 02", Email = "cliente2@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultSeedPassword), Phone = "21911110002", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 03", Email = "cliente3@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultSeedPassword), Phone = "21911110003", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 04", Email = "cliente4@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultSeedPassword), Phone = "21911110004", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 05", Email = "cliente5@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultSeedPassword), Phone = "21911110005", Role = UserRole.Client, IsActive = true }
        };

        await context.Users.AddRangeAsync(providers);
        await context.Users.AddRangeAsync(clients);
        
        if (shouldSeedDefaultAdmin)
        {
            // Seed Default Admin
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Name = "Administrador",
                Email = "admin@teste.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultSeedPassword),
                Phone = "21988887777",
                Role = UserRole.Admin,
                IsActive = true
            };
            await context.Users.AddAsync(admin);
        }

        await context.SaveChangesAsync();
        await EnsureProviderCreditWalletsAsync(context);

        // Seed Service Requests (3 per client) - Status Created
        var categories = new[]
        {
            ServiceCategory.Electrical,
            ServiceCategory.Plumbing,
            ServiceCategory.Electronics,
            ServiceCategory.Appliances,
            ServiceCategory.Masonry,
            ServiceCategory.Cleaning
        };

        var requests = new List<ServiceRequest>();
        var categoryDefinitions = await context.ServiceCategoryDefinitions
            .Where(c => c.IsActive)
            .ToListAsync();
        var categoryByLegacy = categoryDefinitions
            .GroupBy(c => c.LegacyCategory)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var requestGeoPoints = BuildSeedRequestGeoPoints();
        var requestGeoRandom = new Random(20260217);

        for (int i = 0; i < clients.Count; i++)
        {
            for (int j = 1; j <= 3; j++)
            {
                var idx = (i + j) % categories.Length;
                var selectedGeoPoint = requestGeoPoints[requestGeoRandom.Next(requestGeoPoints.Count)];

                requests.Add(new ServiceRequest
                {
                    ClientId = clients[i].Id,
                    Category = categories[idx],
                    CategoryDefinitionId = categoryByLegacy.TryGetValue(categories[idx], out var categoryDefinitionId)
                        ? categoryDefinitionId
                        : null,
                    Description = $"Pedido {j} do {clients[i].Name}",
                    AddressStreet = $"Rua {i + 1}, {100 + j} - {selectedGeoPoint.District}",
                    AddressCity = "Praia Grande",
                    AddressZip = selectedGeoPoint.ZipCode,
                    Latitude = selectedGeoPoint.Latitude,
                    Longitude = selectedGeoPoint.Longitude,
                    Status = ServiceRequestStatus.Created
                });
            }
        }

        await context.ServiceRequests.AddRangeAsync(requests);
        await context.SaveChangesAsync();
        await EnsureSystemSettingsDefaultsAsync(context, configuration);
        await EnsureApiMonitoringSeedAsync(context);
    }

    private static async Task EnsureServiceCategoriesAsync(ConsertaPraMimDbContext context)
    {
        if (await context.ServiceCategoryDefinitions.AnyAsync())
        {
            var existingCategories = await context.ServiceCategoryDefinitions.ToListAsync();
            var hasChanges = false;

            foreach (var category in existingCategories)
            {
                var resolvedIcon = ResolveCategoryIcon(category.LegacyCategory);
                var shouldBackfillIcon =
                    string.IsNullOrWhiteSpace(category.Icon) ||
                    (string.Equals(category.Icon, "build_circle", StringComparison.OrdinalIgnoreCase)
                     && !category.UpdatedAt.HasValue
                     && !string.Equals(resolvedIcon, "build_circle", StringComparison.OrdinalIgnoreCase));

                if (!shouldBackfillIcon)
                {
                    continue;
                }

                category.Icon = resolvedIcon;
                category.UpdatedAt = DateTime.UtcNow;
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
            }

            return;
        }

        var categories = new[]
        {
            new ServiceCategoryDefinition { Name = "Eletrica", Slug = "eletrica", Icon = ResolveCategoryIcon(ServiceCategory.Electrical), LegacyCategory = ServiceCategory.Electrical, IsActive = true },
            new ServiceCategoryDefinition { Name = "Hidraulica", Slug = "hidraulica", Icon = ResolveCategoryIcon(ServiceCategory.Plumbing), LegacyCategory = ServiceCategory.Plumbing, IsActive = true },
            new ServiceCategoryDefinition { Name = "Eletronicos", Slug = "eletronicos", Icon = ResolveCategoryIcon(ServiceCategory.Electronics), LegacyCategory = ServiceCategory.Electronics, IsActive = true },
            new ServiceCategoryDefinition { Name = "Eletrodomesticos", Slug = "eletrodomesticos", Icon = ResolveCategoryIcon(ServiceCategory.Appliances), LegacyCategory = ServiceCategory.Appliances, IsActive = true },
            new ServiceCategoryDefinition { Name = "Alvenaria", Slug = "alvenaria", Icon = ResolveCategoryIcon(ServiceCategory.Masonry), LegacyCategory = ServiceCategory.Masonry, IsActive = true },
            new ServiceCategoryDefinition { Name = "Limpeza", Slug = "limpeza", Icon = ResolveCategoryIcon(ServiceCategory.Cleaning), LegacyCategory = ServiceCategory.Cleaning, IsActive = true },
            new ServiceCategoryDefinition { Name = "Outros", Slug = "outros", Icon = ResolveCategoryIcon(ServiceCategory.Other), LegacyCategory = ServiceCategory.Other, IsActive = true }
        };

        await context.ServiceCategoryDefinitions.AddRangeAsync(categories);
        await context.SaveChangesAsync();
    }

    private static string ResolveCategoryIcon(ServiceCategory legacyCategory)
    {
        return legacyCategory switch
        {
            ServiceCategory.Electrical => "bolt",
            ServiceCategory.Plumbing => "water_drop",
            ServiceCategory.Electronics => "memory",
            ServiceCategory.Appliances => "kitchen",
            ServiceCategory.Masonry => "foundation",
            ServiceCategory.Cleaning => "cleaning_services",
            _ => "build_circle"
        };
    }

    private static async Task EnsurePlanGovernanceDefaultsAsync(ConsertaPraMimDbContext context)
    {
        var allCategories = Enum.GetValues(typeof(ServiceCategory))
            .Cast<ServiceCategory>()
            .OrderBy(x => (int)x)
            .ToList();

        var settingsByPlan = await context.ProviderPlanSettings
            .ToDictionaryAsync(x => x.Plan);

        if (!settingsByPlan.ContainsKey(ProviderPlan.Bronze))
        {
            await context.ProviderPlanSettings.AddAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Bronze,
                MonthlyPrice = 79.90m,
                MaxRadiusKm = 25,
                MaxAllowedCategories = 3,
                AllowedCategories = allCategories.ToList()
            });
        }

        if (!settingsByPlan.ContainsKey(ProviderPlan.Silver))
        {
            await context.ProviderPlanSettings.AddAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Silver,
                MonthlyPrice = 129.90m,
                MaxRadiusKm = 40,
                MaxAllowedCategories = 5,
                AllowedCategories = allCategories.ToList()
            });
        }

        if (!settingsByPlan.ContainsKey(ProviderPlan.Gold))
        {
            await context.ProviderPlanSettings.AddAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Gold,
                MonthlyPrice = 199.90m,
                MaxRadiusKm = 60,
                MaxAllowedCategories = allCategories.Count,
                AllowedCategories = allCategories.ToList()
            });
        }

        if (!await context.ProviderPlanPromotions.AnyAsync())
        {
            await context.ProviderPlanPromotions.AddAsync(new ProviderPlanPromotion
            {
                Plan = ProviderPlan.Bronze,
                Name = "Promocao de Onboarding Bronze",
                DiscountType = PricingDiscountType.Percentage,
                DiscountValue = 10m,
                StartsAtUtc = DateTime.UtcNow.AddDays(-7),
                EndsAtUtc = DateTime.UtcNow.AddDays(30),
                IsActive = true
            });
        }

        if (!await context.ProviderPlanCoupons.AnyAsync())
        {
            await context.ProviderPlanCoupons.AddAsync(new ProviderPlanCoupon
            {
                Code = "BEMVINDO10",
                Name = "Cupom de boas-vindas",
                Plan = null,
                DiscountType = PricingDiscountType.Percentage,
                DiscountValue = 10m,
                StartsAtUtc = DateTime.UtcNow.AddDays(-1),
                EndsAtUtc = DateTime.UtcNow.AddMonths(3),
                MaxGlobalUses = 500,
                MaxUsesPerProvider = 1,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureNoShowRiskPolicyDefaultsAsync(ConsertaPraMimDbContext context)
    {
        if (await context.ServiceAppointmentNoShowRiskPolicies.AnyAsync())
        {
            return;
        }

        await context.ServiceAppointmentNoShowRiskPolicies.AddAsync(new ServiceAppointmentNoShowRiskPolicy
        {
            Name = "Politica padrao de no-show",
            IsActive = true,
            LookbackDays = 90,
            MaxHistoryEventsPerActor = 20,
            MinClientHistoryRiskEvents = 2,
            MinProviderHistoryRiskEvents = 2,
            WeightClientNotConfirmed = 25,
            WeightProviderNotConfirmed = 25,
            WeightBothNotConfirmedBonus = 10,
            WeightWindowWithin24Hours = 10,
            WeightWindowWithin6Hours = 15,
            WeightWindowWithin2Hours = 20,
            WeightClientHistoryRisk = 10,
            WeightProviderHistoryRisk = 10,
            LowThresholdScore = 0,
            MediumThresholdScore = 40,
            HighThresholdScore = 70,
            Notes = "Politica inicial da ST-007. Ajustavel via portal admin."
        });

        await context.SaveChangesAsync();
    }

    private static async Task EnsureNoShowAlertThresholdDefaultsAsync(ConsertaPraMimDbContext context)
    {
        if (await context.NoShowAlertThresholdConfigurations.AnyAsync())
        {
            return;
        }

        await context.NoShowAlertThresholdConfigurations.AddAsync(new NoShowAlertThresholdConfiguration
        {
            Name = "Threshold padrao no-show",
            IsActive = true,
            NoShowRateWarningPercent = 20m,
            NoShowRateCriticalPercent = 30m,
            HighRiskQueueWarningCount = 10,
            HighRiskQueueCriticalCount = 20,
            ReminderSendSuccessWarningPercent = 95m,
            ReminderSendSuccessCriticalPercent = 90m,
            Notes = "Threshold inicial da ST-008 para alertas proativos de no-show."
        });

        await context.SaveChangesAsync();
    }

    private static async Task EnsureServiceFinancialPolicyRuleDefaultsAsync(ConsertaPraMimDbContext context)
    {
        if (await context.ServiceFinancialPolicyRules.AnyAsync())
        {
            return;
        }

        var rules = new List<ServiceFinancialPolicyRule>
        {
            new()
            {
                Name = "Cancelamento do cliente com antecedencia alta (>=24h)",
                EventType = ServiceFinancialPolicyEventType.ClientCancellation,
                MinHoursBeforeWindowStart = 24,
                MaxHoursBeforeWindowStart = null,
                PenaltyPercent = 0m,
                CounterpartyCompensationPercent = 0m,
                PlatformRetainedPercent = 0m,
                Priority = 1,
                IsActive = true,
                Notes = "Sem penalidade para cancelamento com antecedencia."
            },
            new()
            {
                Name = "Cancelamento do cliente com antecedencia media (6h ate 24h)",
                EventType = ServiceFinancialPolicyEventType.ClientCancellation,
                MinHoursBeforeWindowStart = 6,
                MaxHoursBeforeWindowStart = 23,
                PenaltyPercent = 20m,
                CounterpartyCompensationPercent = 15m,
                PlatformRetainedPercent = 5m,
                Priority = 2,
                IsActive = true,
                Notes = "Compensacao parcial ao prestador em cancelamento tardio."
            },
            new()
            {
                Name = "Cancelamento do cliente em cima da hora (<6h)",
                EventType = ServiceFinancialPolicyEventType.ClientCancellation,
                MinHoursBeforeWindowStart = 0,
                MaxHoursBeforeWindowStart = 5,
                PenaltyPercent = 40m,
                CounterpartyCompensationPercent = 30m,
                PlatformRetainedPercent = 10m,
                Priority = 3,
                IsActive = true,
                Notes = "Penalidade elevada para cancelamento muito proximo da janela."
            },
            new()
            {
                Name = "No-show do cliente",
                EventType = ServiceFinancialPolicyEventType.ClientNoShow,
                MinHoursBeforeWindowStart = 0,
                MaxHoursBeforeWindowStart = 0,
                PenaltyPercent = 60m,
                CounterpartyCompensationPercent = 45m,
                PlatformRetainedPercent = 15m,
                Priority = 1,
                IsActive = true,
                Notes = "Cliente ausente na janela confirmada."
            },
            new()
            {
                Name = "Cancelamento do prestador com antecedencia alta (>=24h)",
                EventType = ServiceFinancialPolicyEventType.ProviderCancellation,
                MinHoursBeforeWindowStart = 24,
                MaxHoursBeforeWindowStart = null,
                PenaltyPercent = 0m,
                CounterpartyCompensationPercent = 0m,
                PlatformRetainedPercent = 0m,
                Priority = 1,
                IsActive = true,
                Notes = "Sem penalidade para cancelamento com antecedencia pelo prestador."
            },
            new()
            {
                Name = "Cancelamento do prestador em cima da hora (<24h)",
                EventType = ServiceFinancialPolicyEventType.ProviderCancellation,
                MinHoursBeforeWindowStart = 0,
                MaxHoursBeforeWindowStart = 23,
                PenaltyPercent = 20m,
                CounterpartyCompensationPercent = 15m,
                PlatformRetainedPercent = 5m,
                Priority = 2,
                IsActive = true,
                Notes = "Credito ao cliente por cancelamento tardio do prestador."
            },
            new()
            {
                Name = "No-show do prestador",
                EventType = ServiceFinancialPolicyEventType.ProviderNoShow,
                MinHoursBeforeWindowStart = 0,
                MaxHoursBeforeWindowStart = 0,
                PenaltyPercent = 40m,
                CounterpartyCompensationPercent = 30m,
                PlatformRetainedPercent = 10m,
                Priority = 1,
                IsActive = true,
                Notes = "Prestador ausente na janela confirmada."
            }
        };

        await context.ServiceFinancialPolicyRules.AddRangeAsync(rules);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureChecklistTemplateDefaultsAsync(ConsertaPraMimDbContext context)
    {
        if (await context.ServiceChecklistTemplates.AnyAsync())
        {
            return;
        }

        var categories = await context.ServiceCategoryDefinitions
            .Where(c => c.IsActive)
            .ToListAsync();

        ServiceChecklistTemplate BuildTemplate(
            ServiceCategory legacyCategory,
            string templateName,
            string? description,
            params (string Title, bool IsRequired, bool RequiresEvidence, bool AllowNote)[] items)
        {
            var category = categories.FirstOrDefault(c => c.LegacyCategory == legacyCategory);
            if (category == null)
            {
                throw new InvalidOperationException($"Categoria ativa nao encontrada para checklist padrao: {legacyCategory}.");
            }

            return new ServiceChecklistTemplate
            {
                CategoryDefinitionId = category.Id,
                Name = templateName,
                Description = description,
                IsActive = true,
                Items = items
                    .Select((item, index) => new ServiceChecklistTemplateItem
                    {
                        Title = item.Title,
                        IsRequired = item.IsRequired,
                        RequiresEvidence = item.RequiresEvidence,
                        AllowNote = item.AllowNote,
                        IsActive = true,
                        SortOrder = (index + 1) * 10
                    })
                    .ToList()
            };
        }

        var templates = new List<ServiceChecklistTemplate>
        {
            BuildTemplate(
                ServiceCategory.Electrical,
                "Checklist Eletrica Residencial",
                "Checklist minimo para atendimentos eletricos em ambiente residencial.",
                ("Desligar circuito no quadro geral antes da intervencao", true, false, true),
                ("Testar tensao com instrumento apropriado", true, false, true),
                ("Registrar foto do ponto reparado", true, true, true),
                ("Confirmar funcionamento apos religamento", true, false, true)),

            BuildTemplate(
                ServiceCategory.Plumbing,
                "Checklist Hidraulica",
                "Checklist para servicos de vazamento e manutencao hidraulica.",
                ("Fechar registro geral/local antes do reparo", true, false, true),
                ("Inspecionar conexoes e vedacoes", true, false, true),
                ("Registrar evidencia do reparo concluido", true, true, true),
                ("Testar estanqueidade sem vazamentos", true, false, true)),

            BuildTemplate(
                ServiceCategory.Cleaning,
                "Checklist Limpeza Tecnica",
                "Checklist de qualidade para servicos de limpeza.",
                ("Isolar area e proteger itens sensiveis", true, false, true),
                ("Aplicar procedimento de limpeza combinado", true, false, true),
                ("Registrar foto de resultado final", true, true, true),
                ("Validar area finalizada com cliente", true, false, true))
        };

        await context.ServiceChecklistTemplates.AddRangeAsync(templates);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureProviderCreditWalletsAsync(ConsertaPraMimDbContext context)
    {
        var providerIds = await context.Users
            .Where(x => x.Role == UserRole.Provider)
            .Select(x => x.Id)
            .ToListAsync();

        if (!providerIds.Any())
        {
            return;
        }

        var existingProviderIds = await context.ProviderCreditWallets
            .Where(x => providerIds.Contains(x.ProviderId))
            .Select(x => x.ProviderId)
            .ToListAsync();

        var missingProviderIds = providerIds
            .Except(existingProviderIds)
            .ToList();

        if (!missingProviderIds.Any())
        {
            return;
        }

        var wallets = missingProviderIds
            .Select(providerId => new ProviderCreditWallet
            {
                ProviderId = providerId,
                CurrentBalance = 0m
            })
            .ToList();

        await context.ProviderCreditWallets.AddRangeAsync(wallets);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureSystemSettingsDefaultsAsync(
        ConsertaPraMimDbContext context,
        IConfiguration? configuration)
    {
        var nowUtc = DateTime.UtcNow;
        var defaultTelemetryEnabled = ParseBooleanSetting(configuration?["Monitoring:Enabled"], defaultValue: true);
        var defaultCorsOrigins = GetDefaultCorsAllowedOrigins(configuration);

        var requiredSettings = new Dictionary<string, (string Value, string Description)>(StringComparer.OrdinalIgnoreCase)
        {
            [SystemSettingKeys.MonitoringTelemetryEnabled] = (
                Value: defaultTelemetryEnabled ? "true" : "false",
                Description: "Habilita ou desabilita a captura de telemetria de requests da API."),
            [SystemSettingKeys.CorsAllowedOrigins] = (
                Value: JsonSerializer.Serialize(defaultCorsOrigins),
                Description: "Define a lista de origins permitidas para CORS no backend da API.")
        };

        foreach (var section in RuntimeConfigSections.All)
        {
            requiredSettings[section.SettingKey] = (
                Value: ResolveConfigSectionSeedValue(configuration, section),
                Description: section.Description);
        }

        var keys = requiredSettings.Keys.ToList();
        var existingSettings = await context.SystemSettings
            .Where(x => keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase);

        var hasChanges = false;

        foreach (var (key, seedValue) in requiredSettings)
        {
            if (!existingSettings.TryGetValue(key, out var current))
            {
                await context.SystemSettings.AddAsync(new SystemSetting
                {
                    Key = key,
                    Value = seedValue.Value,
                    Description = seedValue.Description,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
                hasChanges = true;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(current.Value))
            {
                continue;
            }

            current.Value = seedValue.Value;
            current.Description = string.IsNullOrWhiteSpace(current.Description)
                ? seedValue.Description
                : current.Description;
            current.UpdatedAt = nowUtc;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
        }
    }

    private static bool ParseBooleanSetting(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (bool.TryParse(raw.Trim(), out var parsed))
        {
            return parsed;
        }

        return raw.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }

    private static IReadOnlyList<string> GetDefaultCorsAllowedOrigins(IConfiguration? configuration)
    {
        var origins = new List<string>
        {
            "https://localhost:7167",
            "http://localhost:5069",
            "https://localhost:7297",
            "http://localhost:5140",
            "https://localhost:7225",
            "http://localhost:5151",
            "http://localhost:5173",
            "http://localhost:5174",
            "capacitor://localhost",
            "ionic://localhost"
        };

        AddCorsOriginFromUrl(origins, configuration?["Portals:ClientUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["Portals:ProviderUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["Portals:AdminUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:ClientUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:ProviderUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Local:ClientUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Local:ProviderUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Development:ClientUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Development:ProviderUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Vps:ClientUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Vps:ProviderUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Production:ClientUrl"]);
        AddCorsOriginFromUrl(origins, configuration?["AdminPortals:Environments:Production:ProviderUrl"]);

        return origins
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddCorsOriginFromUrl(ICollection<string> origins, string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var parsed))
        {
            return;
        }

        var normalized = parsed.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            origins.Add(normalized.ToLowerInvariant());
        }
    }

    private static string ResolveConfigSectionSeedValue(
        IConfiguration? configuration,
        RuntimeConfigSectionDefinition section)
    {
        var configuredJson = SerializeConfigurationSection(configuration, section.SectionPath);
        if (!string.IsNullOrWhiteSpace(configuredJson))
        {
            return configuredJson;
        }

        return NormalizeJson(section.DefaultJson);
    }

    private static string? SerializeConfigurationSection(
        IConfiguration? configuration,
        string sectionPath)
    {
        if (configuration == null || string.IsNullOrWhiteSpace(sectionPath))
        {
            return null;
        }

        var section = configuration.GetSection(sectionPath);
        if (!section.Exists())
        {
            return null;
        }

        var node = BuildConfigurationNode(section);
        if (node == null)
        {
            return null;
        }

        return JsonSerializer.Serialize(node);
    }

    private static object? BuildConfigurationNode(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            return ParseScalar(section.Value);
        }

        var isArray = children.All(child => int.TryParse(child.Key, out _));
        if (isArray)
        {
            var indexedChildren = children
                .Select(child => new
                {
                    Index = int.Parse(child.Key, CultureInfo.InvariantCulture),
                    Value = BuildConfigurationNode(child)
                })
                .ToList();

            if (indexedChildren.Count == 0)
            {
                return Array.Empty<object?>();
            }

            var maxIndex = indexedChildren.Max(x => x.Index);
            var array = new object?[maxIndex + 1];
            foreach (var item in indexedChildren)
            {
                array[item.Index] = item.Value;
            }

            return array;
        }

        var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in children)
        {
            obj[child.Key] = BuildConfigurationNode(child);
        }

        return obj;
    }

    private static object? ParseScalar(string? rawValue)
    {
        if (rawValue == null)
        {
            return null;
        }

        var value = rawValue.Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        return rawValue;
    }

    private static string NormalizeJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch
        {
            return rawJson.Trim();
        }
    }

    private static async Task EnsureApiMonitoringSeedAsync(ConsertaPraMimDbContext context)
    {
        if (await context.ApiRequestLogs.AnyAsync())
        {
            return;
        }

        var knownUsers = await context.Users
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();

        var random = new Random(20260218);
        var nowUtc = DateTime.UtcNow;
        var templates = new[]
        {
            "api/auth/login",
            "api/service-requests",
            "api/service-requests/{id:guid}",
            "api/proposals",
            "api/chat/{requestId:guid}/{providerId:guid}",
            "api/mobile/client/orders",
            "api/mobile/provider/dashboard",
            "api/admin/dashboard",
            "api/admin/service-requests",
            "api/admin/disputes/queue"
        };

        var methods = new[] { "GET", "POST", "PUT" };
        var logs = new List<ApiRequestLog>(4000);

        for (var index = 0; index < 4000; index++)
        {
            var timestampUtc = nowUtc.AddMinutes(-random.Next(1, 60 * 72));
            var template = templates[random.Next(templates.Length)];
            var method = methods[random.Next(methods.Length)];

            var statusRoll = random.Next(100);
            var statusCode = statusRoll switch
            {
                <= 72 => 200,
                <= 80 => 201,
                <= 88 => 400,
                <= 93 => 401,
                <= 97 => 404,
                _ => 500
            };

            var durationMs = statusCode >= 500
                ? random.Next(900, 3200)
                : statusCode >= 400
                    ? random.Next(350, 1800)
                    : random.Next(40, 900);

            var warningCount = statusCode >= 400 && statusCode < 500
                ? random.Next(1, 3)
                : random.Next(0, 2) == 0 ? 0 : 1;

            var severity = statusCode >= 500
                ? "error"
                : warningCount > 0 || statusCode >= 400
                    ? "warn"
                    : "info";

            var errorType = statusCode switch
            {
                400 => "ValidationException",
                401 => "UnauthorizedAccessException",
                404 => "NotFoundException",
                500 => "UnhandledException",
                _ => null
            };

            var normalizedErrorMessage = statusCode switch
            {
                400 => "requisicao invalida em campo {n}",
                401 => "token ausente ou invalido",
                404 => "registro nao encontrado",
                500 => "falha inesperada na camada de persistencia",
                _ => null
            };

            var normalizedErrorKey = string.IsNullOrWhiteSpace(errorType) || string.IsNullOrWhiteSpace(normalizedErrorMessage)
                ? null
                : BuildSeedErrorKey(errorType, normalizedErrorMessage);

            logs.Add(new ApiRequestLog
            {
                TimestampUtc = timestampUtc,
                CorrelationId = Guid.NewGuid().ToString("D"),
                TraceId = Guid.NewGuid().ToString("N"),
                Method = method,
                EndpointTemplate = template,
                Path = "/" + template.Replace("{id:guid}", Guid.NewGuid().ToString("D")),
                StatusCode = statusCode,
                DurationMs = durationMs,
                Severity = severity,
                IsError = statusCode >= 500,
                WarningCount = warningCount,
                WarningCodesJson = warningCount > 0 ? "[\"validation_warning\"]" : null,
                ErrorType = errorType,
                NormalizedErrorMessage = normalizedErrorMessage,
                NormalizedErrorKey = normalizedErrorKey,
                IpHash = $"SEED-IP-{random.Next(1, 128):D3}",
                UserAgent = "ConsertaPraMim-MonitoringSeed/1.0",
                UserId = knownUsers.Count == 0 ? null : knownUsers[random.Next(knownUsers.Count)],
                TenantId = random.Next(0, 4) == 0 ? "tenant-pg-01" : null,
                RequestSizeBytes = random.Next(100, 25000),
                ResponseSizeBytes = random.Next(120, 64000),
                Scheme = "http",
                Host = "localhost:5193",
                CreatedAt = timestampUtc
            });
        }

        await context.ApiRequestLogs.AddRangeAsync(logs);
        await context.SaveChangesAsync();
    }

    private static string BuildSeedErrorKey(string errorType, string normalizedMessage)
    {
        var payload = $"{errorType}|{normalizedMessage}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes)[..40];
    }

    private static List<User> BuildSeedProviders(string defaultSeedPassword)
    {
        const int providerCount = 20;
        var random = new Random();
        var allCategories = Enum.GetValues(typeof(ServiceCategory))
            .Cast<ServiceCategory>()
            .OrderBy(c => (int)c)
            .ToList();

        var planPool = BuildRandomPlanPool(providerCount, random);
        var providers = new List<User>(providerCount);

        for (var index = 1; index <= providerCount; index++)
        {
            var plan = planPool[index - 1];
            var (maxRadiusKm, maxAllowedCategories) = GetPlanOperationalLimits(plan, allCategories.Count);
            var selectedCategoryCount = random.Next(1, maxAllowedCategories + 1);
            var categories = PickRandomCategories(allCategories, selectedCategoryCount, random);

            var baseLatitude = Math.Round(-22.9068 + ((random.NextDouble() - 0.5) * 0.12), 6);
            var baseLongitude = Math.Round(-43.1729 + ((random.NextDouble() - 0.5) * 0.12), 6);
            var radiusKm = Math.Round(5 + (random.NextDouble() * (maxRadiusKm - 5)), 1);

            providers.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = $"Prestador {index:00}",
                Email = $"prestador{index}@teste.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultSeedPassword),
                Phone = $"2199999{index:0000}",
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    Plan = plan,
                    RadiusKm = radiusKm,
                    BaseZipCode = "11705-270",
                    BaseLatitude = -24.033933309254582,
                    BaseLongitude = -46.50087774134397,
                    Categories = categories
                }
            });
        }

        return providers;
    }

    private static List<ProviderPlan> BuildRandomPlanPool(int providerCount, Random random)
    {
        var managedPlans = new[] { ProviderPlan.Bronze, ProviderPlan.Silver, ProviderPlan.Gold };
        var pool = new List<ProviderPlan>(providerCount);

        // Guarantee at least one provider in each managed plan.
        pool.AddRange(managedPlans);

        while (pool.Count < providerCount)
        {
            pool.Add(managedPlans[random.Next(managedPlans.Length)]);
        }

        return pool
            .OrderBy(_ => random.Next())
            .ToList();
    }

    private static (double MaxRadiusKm, int MaxAllowedCategories) GetPlanOperationalLimits(ProviderPlan plan, int allCategoriesCount)
    {
        return plan switch
        {
            ProviderPlan.Bronze => (25, 3),
            ProviderPlan.Silver => (40, 5),
            ProviderPlan.Gold => (60, allCategoriesCount),
            _ => (10, 1)
        };
    }

    private static List<ServiceCategory> PickRandomCategories(
        IReadOnlyCollection<ServiceCategory> allCategories,
        int take,
        Random random)
    {
        return allCategories
            .OrderBy(_ => random.Next())
            .Take(take)
            .OrderBy(c => (int)c)
            .ToList();
    }

    private static List<SeedRequestGeoPoint> BuildSeedRequestGeoPoints()
    {
        return
        [
            new("Canto do Forte", "11700-310", -24.008857794710888, -46.40434061541606),
            new("Canto do Forte", "11700-310", -24.006013037795, -46.408968980702035),

            new("Boqueirao", "11701-110", -24.008474580103265, -46.41378032453914),
            new("Boqueirao", "11701-850", -24.00768108240361, -46.41415775047435),

            new("Guilhermina", "11701-500", -24.00815245959862, -46.42186845781626),
            new("Guilhermina", "11701-200", -24.011416019080237, -46.42470084545081),

            new("Aviacao", "11702-000", -24.0250, -46.4250),
            new("Aviacao", "11702-200", -24.0271, -46.4232),

            new("Tupi", "11703-000", -24.0312, -46.4320),
            new("Tupi", "11703-200", -24.0330, -46.4301),

            new("Ocian", "11704-000", -24.0365, -46.4385),
            new("Ocian", "11704-300", -24.0380, -46.4362),

            new("Mirim", "11705-000", -24.0422, -46.4445),
            new("Mirim", "11705-200", -24.0440, -46.4420),

            new("Caicara", "11706-000", -24.0495, -46.4540),
            new("Caicara", "11706-200", -24.0510, -46.4515),

            new("Real", "11707-000", -24.0565, -46.4632),
            new("Real", "11707-200", -24.0580, -46.4610),

            new("Solemar", "11709-000", -24.0685, -46.4780),
            new("Solemar", "11709-200", -24.0700, -46.4755)
        ];
    }

    private sealed record SeedRequestGeoPoint(
        string District,
        string ZipCode,
        double Latitude,
        double Longitude);

    private static async Task ClearDatabaseAsync(ConsertaPraMimDbContext context)
    {
        var entityTypes = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.GetTableName() is not null)
            .Select(e => new
            {
                Schema = e.GetSchema() ?? "dbo",
                Table = e.GetTableName()!
            })
            .Distinct()
            .ToList();

        if (!entityTypes.Any()) return;

        var executionStrategy = context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            // Disable all FK constraints, clear data, then re-enable constraints.
            foreach (var t in entityTypes)
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE [{t.Schema}].[{t.Table}] NOCHECK CONSTRAINT ALL;");
            }

            foreach (var t in entityTypes)
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM [{t.Schema}].[{t.Table}];");
            }

            foreach (var t in entityTypes)
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE [{t.Schema}].[{t.Table}] WITH CHECK CHECK CONSTRAINT ALL;");
            }

            await transaction.CommitAsync();
        });
    }

    private static bool IsStrongSeedPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}
