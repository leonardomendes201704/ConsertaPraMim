using System.Diagnostics;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class AdminNoShowDashboardRepositorySqlitePerformanceIntegrationTests
{
    private const int TotalAppointments = 6000;
    private static readonly string[] Cities = { "Santos", "Praia Grande", "Sao Vicente", "Guaruja", "Bertioga" };
    private static readonly ServiceCategory[] Categories =
    {
        ServiceCategory.Electrical,
        ServiceCategory.Plumbing,
        ServiceCategory.Cleaning,
        ServiceCategory.Appliances
    };

    /// <summary>
    /// Cenario: consultas do dashboard de no-show devem suportar alto volume sem degradar experiencia operacional.
    /// Passos: semeia 6.000 agendamentos com variacoes de cidade/categoria/risco e executa consultas com e sem filtros.
    /// Resultado esperado: metricas e listas retornam dados validos e tempos de execucao permanecem abaixo dos budgets.
    /// </summary>
    [Fact(DisplayName = "Admin no show dashboard repository sqlite performance integracao | Dashboard queries | Deve execute within budget on large dataset")]
    public async Task DashboardQueries_ShouldExecuteWithinBudget_OnLargeDataset()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var (rangeStartUtc, rangeEndUtc) = await SeedLargeDatasetAsync(context);
            var repository = new AdminNoShowDashboardRepository(context);

            var unfilteredTimer = Stopwatch.StartNew();
            var unfilteredKpis = await repository.GetKpisAsync(
                rangeStartUtc,
                rangeEndUtc,
                cityFilter: null,
                categoryFilter: null,
                riskLevelFilter: null,
                cancellationNoShowWindowHours: 24);
            var unfilteredByCategory = await repository.GetBreakdownByCategoryAsync(
                rangeStartUtc,
                rangeEndUtc,
                cityFilter: null,
                riskLevelFilter: null,
                cancellationNoShowWindowHours: 24);
            var unfilteredByCity = await repository.GetBreakdownByCityAsync(
                rangeStartUtc,
                rangeEndUtc,
                categoryFilter: null,
                riskLevelFilter: null,
                cancellationNoShowWindowHours: 24);
            var unfilteredQueue = await repository.GetOpenRiskQueueAsync(
                rangeStartUtc,
                rangeEndUtc,
                cityFilter: null,
                categoryFilter: null,
                riskLevelFilter: null,
                take: 100);
            unfilteredTimer.Stop();

            var filteredTimer = Stopwatch.StartNew();
            var filteredKpis = await repository.GetKpisAsync(
                rangeStartUtc,
                rangeEndUtc,
                cityFilter: "santos",
                categoryFilter: "electrical",
                riskLevelFilter: ServiceAppointmentNoShowRiskLevel.High,
                cancellationNoShowWindowHours: 24);
            var filteredByCategory = await repository.GetBreakdownByCategoryAsync(
                rangeStartUtc,
                rangeEndUtc,
                cityFilter: "santos",
                riskLevelFilter: ServiceAppointmentNoShowRiskLevel.High,
                cancellationNoShowWindowHours: 24);
            var filteredByCity = await repository.GetBreakdownByCityAsync(
                rangeStartUtc,
                rangeEndUtc,
                categoryFilter: "electrical",
                riskLevelFilter: ServiceAppointmentNoShowRiskLevel.High,
                cancellationNoShowWindowHours: 24);
            var filteredQueue = await repository.GetOpenRiskQueueAsync(
                rangeStartUtc,
                rangeEndUtc,
                cityFilter: "santos",
                categoryFilter: "electrical",
                riskLevelFilter: ServiceAppointmentNoShowRiskLevel.High,
                take: 50);
            filteredTimer.Stop();

            Assert.True(unfilteredKpis.BaseAppointments > 0);
            Assert.NotEmpty(unfilteredByCategory);
            Assert.NotEmpty(unfilteredByCity);
            Assert.True(unfilteredQueue.Count <= 100);
            Assert.True(unfilteredTimer.Elapsed < TimeSpan.FromSeconds(8),
                $"Consultas sem filtro excederam budget: {unfilteredTimer.Elapsed.TotalMilliseconds:N0} ms.");

            Assert.True(filteredKpis.BaseAppointments > 0);
            Assert.NotEmpty(filteredByCategory);
            Assert.NotEmpty(filteredByCity);
            Assert.True(filteredQueue.Count <= 50);
            Assert.True(filteredTimer.Elapsed < TimeSpan.FromSeconds(6),
                $"Consultas com filtro excederam budget: {filteredTimer.Elapsed.TotalMilliseconds:N0} ms.");
        }
    }

    private static async Task<(DateTime RangeStartUtc, DateTime RangeEndUtc)> SeedLargeDatasetAsync(
        ConsertaPraMim.Infrastructure.Data.ConsertaPraMimDbContext context)
    {
        var clients = Enumerable.Range(1, 120)
            .Select(i => new User
            {
                Name = $"Cliente {i:000}",
                Email = $"cliente.perf{i:000}@teste.com",
                PasswordHash = "hash",
                Phone = "11999999999",
                Role = UserRole.Client
            })
            .ToList();

        var providers = Enumerable.Range(1, 80)
            .Select(i => new User
            {
                Name = $"Prestador {i:000}",
                Email = $"prestador.perf{i:000}@teste.com",
                PasswordHash = "hash",
                Phone = "11888888888",
                Role = UserRole.Provider
            })
            .ToList();

        context.Users.AddRange(clients);
        context.Users.AddRange(providers);

        var requests = new List<ServiceRequest>(TotalAppointments);
        var appointments = new List<ServiceAppointment>(TotalAppointments);
        var queueItems = new List<ServiceAppointmentNoShowQueueItem>(TotalAppointments / 4);

        var baseDateUtc = DateTime.UtcNow.Date.AddDays(-20);

        for (var i = 0; i < TotalAppointments; i++)
        {
            var client = clients[i % clients.Count];
            var provider = providers[i % providers.Count];
            var city = Cities[i % Cities.Length];
            var category = Categories[i % Categories.Length];
            var windowStartUtc = baseDateUtc.AddDays(i % 35).AddHours(i % 24);

            var request = new ServiceRequest
            {
                ClientId = client.Id,
                Category = category,
                Status = ServiceRequestStatus.Created,
                Description = $"Pedido de performance {i:00000}",
                AddressStreet = $"Rua Perf {i:00000}",
                AddressCity = city,
                AddressZip = "11704150",
                Latitude = -24.010 + ((i % 30) * 0.001),
                Longitude = -46.410 + ((i % 30) * 0.001)
            };
            requests.Add(request);

            var statusSelector = i % 8;
            var status = statusSelector switch
            {
                0 => ServiceAppointmentStatus.ExpiredWithoutProviderAction,
                1 => ServiceAppointmentStatus.CancelledByClient,
                2 => ServiceAppointmentStatus.CancelledByProvider,
                3 => ServiceAppointmentStatus.Arrived,
                4 => ServiceAppointmentStatus.Completed,
                5 => ServiceAppointmentStatus.InProgress,
                6 => ServiceAppointmentStatus.Confirmed,
                _ => ServiceAppointmentStatus.RescheduleConfirmed
            };

            DateTime? cancelledAtUtc = status is ServiceAppointmentStatus.CancelledByClient or ServiceAppointmentStatus.CancelledByProvider
                ? windowStartUtc.AddHours(i % 2 == 0 ? -8 : -36)
                : null;

            var riskLevel = (i % 3) switch
            {
                0 => ServiceAppointmentNoShowRiskLevel.High,
                1 => ServiceAppointmentNoShowRiskLevel.Medium,
                _ => ServiceAppointmentNoShowRiskLevel.Low
            };

            var appointment = new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = windowStartUtc,
                WindowEndUtc = windowStartUtc.AddHours(1),
                Status = status,
                CancelledAtUtc = cancelledAtUtc,
                ClientPresenceConfirmed = i % 2 == 0,
                ProviderPresenceConfirmed = i % 4 == 0,
                NoShowRiskLevel = riskLevel,
                NoShowRiskScore = riskLevel switch
                {
                    ServiceAppointmentNoShowRiskLevel.High => 85,
                    ServiceAppointmentNoShowRiskLevel.Medium => 55,
                    _ => 20
                },
                NoShowRiskReasons = riskLevel switch
                {
                    ServiceAppointmentNoShowRiskLevel.High => "both_presence_not_confirmed,window_within_2h",
                    ServiceAppointmentNoShowRiskLevel.Medium => "client_presence_not_confirmed,window_within_6h",
                    _ => "none"
                },
                NoShowRiskCalculatedAtUtc = windowStartUtc.AddHours(-2)
            };
            appointments.Add(appointment);

            if (i % 4 == 0)
            {
                queueItems.Add(new ServiceAppointmentNoShowQueueItem
                {
                    ServiceAppointmentId = appointment.Id,
                    RiskLevel = i % 8 == 0 ? ServiceAppointmentNoShowRiskLevel.High : ServiceAppointmentNoShowRiskLevel.Medium,
                    Score = i % 8 == 0 ? 92 : 60,
                    ReasonsCsv = "both_presence_not_confirmed,window_within_2h",
                    Status = i % 10 == 0 ? ServiceAppointmentNoShowQueueStatus.InProgress : ServiceAppointmentNoShowQueueStatus.Open,
                    FirstDetectedAtUtc = windowStartUtc.AddHours(-3),
                    LastDetectedAtUtc = windowStartUtc.AddHours(-1)
                });
            }
        }

        context.ServiceRequests.AddRange(requests);
        context.ServiceAppointments.AddRange(appointments);
        context.ServiceAppointmentNoShowQueueItems.AddRange(queueItems);
        await context.SaveChangesAsync();

        return (baseDateUtc, baseDateUtc.AddDays(35));
    }
}
