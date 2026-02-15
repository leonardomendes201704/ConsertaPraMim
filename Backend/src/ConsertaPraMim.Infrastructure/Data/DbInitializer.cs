using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        if (!seedEnabled)
        {
            return;
        }

        var shouldResetDatabase = configuration?.GetValue<bool?>("Seed:Reset") ?? false;
        var shouldSeedDefaultAdmin = configuration?.GetValue<bool?>("Seed:CreateDefaultAdmin")
            ?? hostEnvironment?.IsDevelopment() == true;
        var defaultSeedPassword = configuration?["Seed:DefaultPassword"] ?? "SeedDev!2026";
        if (!IsStrongSeedPassword(defaultSeedPassword))
        {
            throw new InvalidOperationException("Seed:DefaultPassword invalida. Use ao menos 8 caracteres com maiuscula, minuscula, numero e caractere especial.");
        }

        var executionStrategy = context.Database.CreateExecutionStrategy();

        // Apply migrations if any using SQL resiliency strategy.
        await executionStrategy.ExecuteAsync(async () =>
        {
            await context.Database.MigrateAsync();
        });

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
        await EnsureProviderCreditWalletsAsync(context);

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

        var baseLat = -22.9100;
        var baseLng = -43.1800;

        for (int i = 0; i < clients.Count; i++)
        {
            for (int j = 1; j <= 3; j++)
            {
                var idx = (i + j) % categories.Length;
                requests.Add(new ServiceRequest
                {
                    ClientId = clients[i].Id,
                    Category = categories[idx],
                    CategoryDefinitionId = categoryByLegacy.TryGetValue(categories[idx], out var categoryDefinitionId)
                        ? categoryDefinitionId
                        : null,
                    Description = $"Pedido {j} do {clients[i].Name}",
                    AddressStreet = $"Rua {i + 1}, {100 + j}",
                    AddressCity = "Rio de Janeiro",
                    AddressZip = "20000-000",
                    Latitude = baseLat + (i * 0.002) + (j * 0.001),
                    Longitude = baseLng - (i * 0.002) - (j * 0.001),
                    Status = ServiceRequestStatus.Created
                });
            }
        }

        await context.ServiceRequests.AddRangeAsync(requests);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureServiceCategoriesAsync(ConsertaPraMimDbContext context)
    {
        if (await context.ServiceCategoryDefinitions.AnyAsync())
        {
            return;
        }

        var categories = new[]
        {
            new ServiceCategoryDefinition { Name = "Eletrica", Slug = "eletrica", LegacyCategory = ServiceCategory.Electrical, IsActive = true },
            new ServiceCategoryDefinition { Name = "Hidraulica", Slug = "hidraulica", LegacyCategory = ServiceCategory.Plumbing, IsActive = true },
            new ServiceCategoryDefinition { Name = "Eletronicos", Slug = "eletronicos", LegacyCategory = ServiceCategory.Electronics, IsActive = true },
            new ServiceCategoryDefinition { Name = "Eletrodomesticos", Slug = "eletrodomesticos", LegacyCategory = ServiceCategory.Appliances, IsActive = true },
            new ServiceCategoryDefinition { Name = "Alvenaria", Slug = "alvenaria", LegacyCategory = ServiceCategory.Masonry, IsActive = true },
            new ServiceCategoryDefinition { Name = "Limpeza", Slug = "limpeza", LegacyCategory = ServiceCategory.Cleaning, IsActive = true },
            new ServiceCategoryDefinition { Name = "Outros", Slug = "outros", LegacyCategory = ServiceCategory.Other, IsActive = true }
        };

        await context.ServiceCategoryDefinitions.AddRangeAsync(categories);
        await context.SaveChangesAsync();
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
                    BaseZipCode = "20000-000",
                    BaseLatitude = baseLatitude,
                    BaseLongitude = baseLongitude,
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
