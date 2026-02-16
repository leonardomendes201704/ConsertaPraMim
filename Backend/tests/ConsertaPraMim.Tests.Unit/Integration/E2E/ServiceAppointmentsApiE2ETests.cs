using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace ConsertaPraMim.Tests.Unit.Integration.E2E;

public class ServiceAppointmentsApiE2ETests
{
    [Fact]
    public async Task Slots_Create_And_Mine_ShouldWork_EndToEnd()
    {
        await using var factory = new ServiceAppointmentsApiFactory();

        var scenario = await factory.SeedScenarioAsync(includeBookedAppointmentForOtherRequest: false);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, scenario.ClientId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, UserRole.Client.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeaderName, "cliente.e2e@teste.com");

        var slotsResponse = await client.GetAsync(
            $"/api/service-appointments/slots?providerId={scenario.ProviderId}&fromUtc={Uri.EscapeDataString(scenario.RangeStartUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(scenario.RangeEndUtc.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, slotsResponse.StatusCode);

        var slots = await slotsResponse.Content.ReadFromJsonAsync<List<ServiceAppointmentSlotDto>>();
        Assert.NotNull(slots);
        var selectedSlot = slots.SingleOrDefault(slot =>
            slot.WindowStartUtc == scenario.ExpectedSlotStartUtc &&
            slot.WindowEndUtc == scenario.ExpectedSlotEndUtc);
        Assert.NotNull(selectedSlot);

        var createResponse = await client.PostAsJsonAsync(
            "/api/service-appointments",
            new CreateServiceAppointmentRequestDto(
                scenario.TargetRequestId,
                scenario.ProviderId,
                selectedSlot.WindowStartUtc,
                selectedSlot.WindowEndUtc,
                "E2E - agendamento de teste"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ServiceAppointmentDto>();
        Assert.NotNull(created);
        Assert.Equal(ServiceAppointmentStatus.PendingProviderConfirmation.ToString(), created.Status);
        Assert.Equal(scenario.TargetRequestId, created.ServiceRequestId);
        Assert.Equal(scenario.ProviderId, created.ProviderId);
        Assert.Equal(scenario.ClientId, created.ClientId);

        var mineResponse = await client.GetAsync("/api/service-appointments/mine");
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);

        var mineAppointments = await mineResponse.Content.ReadFromJsonAsync<List<ServiceAppointmentDto>>();
        Assert.NotNull(mineAppointments);
        Assert.Contains(mineAppointments, appointment => appointment.Id == created.Id);
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenProviderSlotIsAlreadyBooked()
    {
        await using var factory = new ServiceAppointmentsApiFactory();

        var scenario = await factory.SeedScenarioAsync(includeBookedAppointmentForOtherRequest: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, scenario.ClientId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, UserRole.Client.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeaderName, "cliente.e2e@teste.com");

        var createResponse = await client.PostAsJsonAsync(
            "/api/service-appointments",
            new CreateServiceAppointmentRequestDto(
                scenario.TargetRequestId,
                scenario.ProviderId,
                scenario.ExpectedSlotStartUtc,
                scenario.ExpectedSlotEndUtc,
                "E2E - tentativa de conflito"));

        Assert.Equal(HttpStatusCode.Conflict, createResponse.StatusCode);

        var payload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        Assert.Equal("slot_unavailable", payload.RootElement.GetProperty("errorCode").GetString());
    }

    private sealed record SeedScenario(
        Guid ClientId,
        Guid ProviderId,
        Guid TargetRequestId,
        DateTime RangeStartUtc,
        DateTime RangeEndUtc,
        DateTime ExpectedSlotStartUtc,
        DateTime ExpectedSlotEndUtc);

    private sealed class ServiceAppointmentsApiFactory : WebApplicationFactory<Program>
    {
        private SqliteConnection? _connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:Enabled"] = "false",
                    ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=ConsertaPraMim.Tests;Trusted_Connection=True;",
                    ["ServiceAppointments:AvailabilityTimeZoneId"] = "UTC"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ConsertaPraMimDbContext>>();
                services.RemoveAll<ConsertaPraMimDbContext>();
                var dbContextOptionConfigurations = services
                    .Where(descriptor =>
                        descriptor.ServiceType.IsGenericType &&
                        descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>) &&
                        descriptor.ServiceType.GenericTypeArguments[0] == typeof(ConsertaPraMimDbContext))
                    .ToList();
                foreach (var descriptor in dbContextOptionConfigurations)
                {
                    services.Remove(descriptor);
                }

                var backgroundWorkers = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Namespace == "ConsertaPraMim.API.BackgroundJobs")
                    .ToList();
                foreach (var descriptor in backgroundWorkers)
                {
                    services.Remove(descriptor);
                }

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                services.AddSingleton(_connection);
                services.AddDbContext<ConsertaPraMimDbContext>((serviceProvider, options) =>
                {
                    options.UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>());
                });

                using var serviceScope = services.BuildServiceProvider().CreateScope();
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
                dbContext.Database.EnsureCreated();
            });
        }

        public async Task<SeedScenario> SeedScenarioAsync(bool includeBookedAppointmentForOtherRequest)
        {
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();

            var provider = new User
            {
                Id = Guid.NewGuid(),
                Name = "Prestador E2E",
                Email = "prestador.e2e@teste.com",
                PasswordHash = "hash",
                Phone = "11900000000",
                Role = UserRole.Provider,
                IsActive = true
            };

            var client = new User
            {
                Id = Guid.NewGuid(),
                Name = "Cliente E2E",
                Email = "cliente.e2e@teste.com",
                PasswordHash = "hash",
                Phone = "11911111111",
                Role = UserRole.Client,
                IsActive = true
            };

            var baseDateUtc = DateTime.UtcNow.Date.AddDays(2);
            var expectedSlotStartUtc = baseDateUtc.AddHours(10);
            var expectedSlotEndUtc = expectedSlotStartUtc.AddHours(1);

            var targetRequest = new ServiceRequest
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                Category = ServiceCategory.Electrical,
                Status = ServiceRequestStatus.Created,
                Description = "Pedido E2E para agendamento",
                AddressStreet = "Rua Teste, 100",
                AddressCity = "Sao Paulo",
                AddressZip = "01001000",
                Latitude = -23.55,
                Longitude = -46.63
            };

            var targetProposal = new Proposal
            {
                Id = Guid.NewGuid(),
                RequestId = targetRequest.Id,
                ProviderId = provider.Id,
                Accepted = true,
                IsInvalidated = false,
                Message = "Proposta aceita para E2E"
            };

            var availabilityRule = new ProviderAvailabilityRule
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                DayOfWeek = baseDateUtc.DayOfWeek,
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(12),
                SlotDurationMinutes = 60,
                IsActive = true
            };

            context.Users.AddRange(provider, client);
            context.ServiceRequests.Add(targetRequest);
            context.Proposals.Add(targetProposal);
            context.ProviderAvailabilityRules.Add(availabilityRule);

            if (includeBookedAppointmentForOtherRequest)
            {
                var bookedRequest = new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    Category = ServiceCategory.Electrical,
                    Status = ServiceRequestStatus.Created,
                    Description = "Pedido E2E com slot bloqueado",
                    AddressStreet = "Rua Teste, 200",
                    AddressCity = "Sao Paulo",
                    AddressZip = "01001001",
                    Latitude = -23.56,
                    Longitude = -46.64
                };

                var bookedProposal = new Proposal
                {
                    Id = Guid.NewGuid(),
                    RequestId = bookedRequest.Id,
                    ProviderId = provider.Id,
                    Accepted = true,
                    IsInvalidated = false,
                    Message = "Proposta aceita para bloqueio"
                };

                var bookedAppointment = new ServiceAppointment
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = bookedRequest.Id,
                    ClientId = client.Id,
                    ProviderId = provider.Id,
                    WindowStartUtc = expectedSlotStartUtc,
                    WindowEndUtc = expectedSlotEndUtc,
                    Status = ServiceAppointmentStatus.Confirmed,
                    OperationalStatus = ServiceAppointmentOperationalStatus.OnTheWay,
                    ConfirmedAtUtc = DateTime.UtcNow
                };

                context.ServiceRequests.Add(bookedRequest);
                context.Proposals.Add(bookedProposal);
                context.ServiceAppointments.Add(bookedAppointment);
            }

            await context.SaveChangesAsync();

            return new SeedScenario(
                client.Id,
                provider.Id,
                targetRequest.Id,
                baseDateUtc,
                baseDateUtc.AddDays(1),
                expectedSlotStartUtc,
                expectedSlotEndUtc);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestAuth";
        public const string UserIdHeaderName = "X-Test-UserId";
        public const string RoleHeaderName = "X-Test-Role";
        public const string EmailHeaderName = "X-Test-Email";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userIdRaw = Request.Headers[UserIdHeaderName].FirstOrDefault();
            var role = Request.Headers[RoleHeaderName].FirstOrDefault();
            var email = Request.Headers[EmailHeaderName].FirstOrDefault() ?? "test@localhost";

            if (!Guid.TryParse(userIdRaw, out var userId) || string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing test authentication headers."));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Email, email)
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
