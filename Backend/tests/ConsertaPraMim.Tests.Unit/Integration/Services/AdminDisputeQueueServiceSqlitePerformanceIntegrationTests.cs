using System.Diagnostics;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class AdminDisputeQueueServiceSqlitePerformanceIntegrationTests
{
    private const int TotalDisputes = 4000;
    private static readonly ServiceCategory[] Categories =
    {
        ServiceCategory.Electrical,
        ServiceCategory.Plumbing,
        ServiceCategory.Electronics,
        ServiceCategory.Appliances,
        ServiceCategory.Masonry,
        ServiceCategory.Cleaning
    };
    private static readonly DisputeCaseType[] DisputeTypes =
    {
        DisputeCaseType.Billing,
        DisputeCaseType.ServiceQuality,
        DisputeCaseType.Conduct,
        DisputeCaseType.NoShow
    };
    private static readonly string[] Reasons =
    {
        "PRICE_DIVERGENCE",
        "NO_SHOW",
        "QUALITY",
        "CONDUCT",
        "PAYMENT_ISSUE"
    };

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dispute queue servico sqlite performance integracao | Panel queries | Deve execute within budget on large dataset.
    /// </summary>
    [Fact(DisplayName = "Admin dispute queue servico sqlite performance integracao | Panel queries | Deve execute within budget on large dataset")]
    public async Task PanelQueries_ShouldExecuteWithinBudget_OnLargeDataset()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var seeded = await SeedLargeDatasetAsync(context);
            var service = BuildService(context);

            var unfilteredTimer = Stopwatch.StartNew();
            var queue = await service.GetQueueAsync(
                highlightedDisputeCaseId: null,
                take: 200,
                status: null,
                type: null,
                operatorAdminId: null,
                operatorScope: "all",
                sla: "all");
            var observability = await service.GetObservabilityAsync(new AdminDisputeObservabilityQueryDto(
                seeded.RangeStartUtc,
                seeded.RangeEndUtc,
                TopTake: 12));
            var auditTrail = await service.GetAuditTrailAsync(new AdminDisputeAuditQueryDto(
                seeded.RangeStartUtc,
                seeded.RangeEndUtc,
                ActorUserId: null,
                DisputeCaseId: null,
                EventType: null,
                Take: 800));
            unfilteredTimer.Stop();

            var filteredTimer = Stopwatch.StartNew();
            var filteredQueue = await service.GetQueueAsync(
                highlightedDisputeCaseId: seeded.SampleDisputeId,
                take: 100,
                status: "Open",
                type: "Billing",
                operatorAdminId: seeded.PrimaryAdminId,
                operatorScope: "assigned",
                sla: "ontrack");
            var filteredObservability = await service.GetObservabilityAsync(new AdminDisputeObservabilityQueryDto(
                seeded.RangeStartUtc,
                seeded.RangeEndUtc,
                TopTake: 5));
            var filteredAuditTrail = await service.GetAuditTrailAsync(new AdminDisputeAuditQueryDto(
                seeded.RangeStartUtc,
                seeded.RangeEndUtc,
                seeded.PrimaryAdminId,
                seeded.SampleDisputeId,
                EventType: "dispute_case_viewed",
                Take: 100));
            filteredTimer.Stop();

            Assert.NotNull(queue);
            Assert.NotEmpty(queue.Items);
            Assert.True(observability.TotalDisputesOpened >= TotalDisputes);
            Assert.NotEmpty(auditTrail.Items);
            Assert.True(unfilteredTimer.Elapsed < TimeSpan.FromSeconds(12),
                $"Consultas de painel sem filtro excederam budget: {unfilteredTimer.Elapsed.TotalMilliseconds:N0} ms.");

            Assert.NotNull(filteredQueue);
            Assert.True(filteredQueue.Items.Count <= 100);
            Assert.True(filteredObservability.TotalDisputesOpened >= TotalDisputes);
            Assert.NotEmpty(filteredAuditTrail.Items);
            Assert.True(filteredTimer.Elapsed < TimeSpan.FromSeconds(9),
                $"Consultas de painel com filtro excederam budget: {filteredTimer.Elapsed.TotalMilliseconds:N0} ms.");
        }
    }

    private static AdminDisputeQueueService BuildService(ConsertaPraMimDbContext context)
    {
        return new AdminDisputeQueueService(
            new ServiceDisputeCaseRepository(context),
            new UserRepository(context),
            new AdminAuditLogRepository(context),
            Mock.Of<IServicePaymentTransactionRepository>(),
            Mock.Of<IPaymentService>(),
            Mock.Of<IProviderCreditService>(),
            Mock.Of<INotificationService>());
    }

    private static async Task<SeededDisputeDataset> SeedLargeDatasetAsync(ConsertaPraMimDbContext context)
    {
        var clients = Enumerable.Range(1, 200)
            .Select(i => new User
            {
                Name = $"Cliente Perf {i:000}",
                Email = $"cliente.dispute.perf{i:000}@teste.com",
                PasswordHash = "hash",
                Phone = "11999999999",
                Role = UserRole.Client
            })
            .ToList();

        var providers = Enumerable.Range(1, 130)
            .Select(i => new User
            {
                Name = $"Prestador Perf {i:000}",
                Email = $"prestador.dispute.perf{i:000}@teste.com",
                PasswordHash = "hash",
                Phone = "11888888888",
                Role = UserRole.Provider
            })
            .ToList();

        var admins = Enumerable.Range(1, 8)
            .Select(i => new User
            {
                Name = $"Admin Perf {i:000}",
                Email = $"admin.dispute.perf{i:000}@teste.com",
                PasswordHash = "hash",
                Phone = "11777777777",
                Role = UserRole.Admin
            })
            .ToList();

        context.Users.AddRange(clients);
        context.Users.AddRange(providers);
        context.Users.AddRange(admins);

        var serviceRequests = new List<ServiceRequest>(TotalDisputes);
        var serviceAppointments = new List<ServiceAppointment>(TotalDisputes);
        var disputeCases = new List<ServiceDisputeCase>(TotalDisputes);
        var caseAuditEntries = new List<ServiceDisputeCaseAuditEntry>(TotalDisputes * 2);
        var adminAuditLogs = new List<AdminAuditLog>(TotalDisputes);

        var baseDateUtc = DateTime.UtcNow.Date.AddDays(-45);
        var primaryAdmin = admins[0];
        Guid sampleDisputeId = Guid.Empty;

        for (var i = 0; i < TotalDisputes; i++)
        {
            var client = clients[i % clients.Count];
            var provider = providers[i % providers.Count];
            var admin = admins[i % admins.Count];
            var category = Categories[i % Categories.Length];
            var disputeType = DisputeTypes[i % DisputeTypes.Length];
            var reason = Reasons[i % Reasons.Length];

            var openedAtUtc = baseDateUtc.AddHours(i % 1000);
            var status = (i % 5) switch
            {
                0 => DisputeCaseStatus.Open,
                1 => DisputeCaseStatus.UnderReview,
                2 => DisputeCaseStatus.WaitingParties,
                3 => DisputeCaseStatus.Resolved,
                _ => DisputeCaseStatus.Rejected
            };

            if (i == 0)
            {
                status = DisputeCaseStatus.Open;
                disputeType = DisputeCaseType.Billing;
                admin = primaryAdmin;
            }

            var serviceRequest = new ServiceRequest
            {
                ClientId = client.Id,
                Category = category,
                Status = ServiceRequestStatus.Scheduled,
                Description = $"Disputa de performance {i:00000}",
                AddressStreet = $"Rua Perf Disputa {i:00000}",
                AddressCity = i % 2 == 0 ? "Santos" : "Praia Grande",
                AddressZip = "11704150",
                Latitude = -24.01 + ((i % 50) * 0.001),
                Longitude = -46.41 + ((i % 50) * 0.001)
            };
            serviceRequests.Add(serviceRequest);

            var serviceAppointment = new ServiceAppointment
            {
                ServiceRequestId = serviceRequest.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = openedAtUtc.AddDays(2),
                WindowEndUtc = openedAtUtc.AddDays(2).AddHours(1),
                Status = ServiceAppointmentStatus.Confirmed
            };
            serviceAppointments.Add(serviceAppointment);

            DateTime? closedAtUtc = status is DisputeCaseStatus.Resolved or DisputeCaseStatus.Rejected
                ? openedAtUtc.AddHours((i % 12) + 1)
                : null;

            var disputeCase = new ServiceDisputeCase
            {
                ServiceRequestId = serviceRequest.Id,
                ServiceAppointmentId = serviceAppointment.Id,
                OpenedByUserId = client.Id,
                OpenedByRole = ServiceAppointmentActorRole.Client,
                CounterpartyUserId = provider.Id,
                CounterpartyRole = ServiceAppointmentActorRole.Provider,
                OwnedByAdminUserId = status is DisputeCaseStatus.Resolved or DisputeCaseStatus.Rejected ? admin.Id : admin.Id,
                OwnedAtUtc = openedAtUtc.AddMinutes(30),
                Type = disputeType,
                Priority = (i % 4) switch
                {
                    0 => DisputeCasePriority.Critical,
                    1 => DisputeCasePriority.High,
                    2 => DisputeCasePriority.Medium,
                    _ => DisputeCasePriority.Low
                },
                Status = status,
                WaitingForRole = status == DisputeCaseStatus.WaitingParties
                    ? ServiceAppointmentActorRole.Provider
                    : null,
                ReasonCode = reason,
                Description = $"Descricao de disputa {i:00000}",
                OpenedAtUtc = openedAtUtc,
                SlaDueAtUtc = openedAtUtc.AddHours(24),
                LastInteractionAtUtc = openedAtUtc.AddHours(2),
                ClosedAtUtc = closedAtUtc,
                ResolutionSummary = closedAtUtc.HasValue ? "Resolucao de performance" : null,
                MetadataJson = closedAtUtc.HasValue
                    ? "{\"outcome\":\"procedente\"}"
                    : null
            };
            disputeCases.Add(disputeCase);

            if (i == 0)
            {
                sampleDisputeId = disputeCase.Id;
            }

            caseAuditEntries.Add(new ServiceDisputeCaseAuditEntry
            {
                ServiceDisputeCaseId = disputeCase.Id,
                ActorUserId = admin.Id,
                ActorRole = ServiceAppointmentActorRole.Admin,
                EventType = "dispute_case_viewed",
                Message = "Visualizacao administrativa",
                MetadataJson = "{\"source\":\"perf_seed\"}",
                CreatedAt = openedAtUtc.AddMinutes(10)
            });

            caseAuditEntries.Add(new ServiceDisputeCaseAuditEntry
            {
                ServiceDisputeCaseId = disputeCase.Id,
                ActorUserId = admin.Id,
                ActorRole = ServiceAppointmentActorRole.Admin,
                EventType = "dispute_workflow_updated",
                Message = "Workflow atualizado",
                MetadataJson = "{\"source\":\"perf_seed\"}",
                CreatedAt = openedAtUtc.AddMinutes(20)
            });

            adminAuditLogs.Add(new AdminAuditLog
            {
                ActorUserId = admin.Id,
                ActorEmail = admin.Email,
                Action = status is DisputeCaseStatus.Resolved or DisputeCaseStatus.Rejected
                    ? "DisputeDecisionRecorded"
                    : "DisputeCaseViewed",
                TargetType = "ServiceDisputeCase",
                TargetId = disputeCase.Id,
                Metadata = "{\"source\":\"perf_seed\"}",
                CreatedAt = openedAtUtc.AddMinutes(30)
            });
        }

        context.ServiceRequests.AddRange(serviceRequests);
        context.ServiceAppointments.AddRange(serviceAppointments);
        context.ServiceDisputeCases.AddRange(disputeCases);
        context.ServiceDisputeCaseAuditEntries.AddRange(caseAuditEntries);
        context.AdminAuditLogs.AddRange(adminAuditLogs);
        await context.SaveChangesAsync();

        return new SeededDisputeDataset(
            baseDateUtc,
            baseDateUtc.AddDays(60),
            primaryAdmin.Id,
            sampleDisputeId);
    }

    private sealed record SeededDisputeDataset(
        DateTime RangeStartUtc,
        DateTime RangeEndUtc,
        Guid PrimaryAdminId,
        Guid SampleDisputeId);
}
