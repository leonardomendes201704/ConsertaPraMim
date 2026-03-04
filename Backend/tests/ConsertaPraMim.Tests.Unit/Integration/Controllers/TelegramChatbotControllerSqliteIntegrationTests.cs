using System.Security.Claims;
using ConsertaPraMim.API.Contracts;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Controllers;

public class TelegramChatbotControllerSqliteIntegrationTests
{
    /// <summary>
    /// Cenario: seguranca do endpoint de chatbot Telegram exposto na API.
    /// Passos: inspeciona atributos do controller por reflexao.
    /// Resultado esperado: acesso restrito a usuarios com role Client.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot controller sqlite integracao | Controller | Deve exigir role client")]
    public void Controller_ShouldRequireClientRole()
    {
        var authorize = typeof(TelegramChatbotController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("Client", authorize!.Roles);
    }

    /// <summary>
    /// Cenario: cliente A cria conversa no chatbot e cliente B tenta consultar o historico da mesma conversa.
    /// Passos: abre sessao autenticado como cliente A e consulta historico autenticado como cliente B.
    /// Resultado esperado: endpoint retorna NotFound para impedir acesso cruzado entre clientes.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot controller sqlite integracao | Historico | Deve bloquear acesso cruzado por clientId")]
    public async Task GetConversationHistory_ShouldReturnNotFound_WhenConversationBelongsToAnotherClient()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var repository = new ChatbotConversationRepository(context);
            var service = new TelegramChatbotConversationService(repository);
            var controller = new TelegramChatbotController(service);

            var ownerClientId = Guid.NewGuid();
            await SeedClientAsync(context, ownerClientId);
            controller.ControllerContext = BuildClientControllerContext(ownerClientId);

            var openResult = await controller.OpenSession(new TelegramChatbotOpenSessionRequest
            {
                Channel = "telegram",
                ChannelConversationId = "chat-access-control"
            });

            var openOk = Assert.IsType<OkObjectResult>(openResult);
            var createdConversation = Assert.IsType<TelegramChatbotConversationDto>(openOk.Value);

            controller.ControllerContext = BuildClientControllerContext(Guid.NewGuid());
            var historyResult = await controller.GetConversationHistory(createdConversation.Id);

            Assert.IsType<NotFoundObjectResult>(historyResult);
        }
    }

    /// <summary>
    /// Cenario: datas informadas no horario local entram no fluxo de sessao/mensagem do chatbot.
    /// Passos: cria sessao e mensagem com DateTime local, consulta persistencia SQLite e historico retornado pelo endpoint.
    /// Resultado esperado: valores persistidos e retornados em UTC, sem conversao para America/Sao_Paulo no contrato da API.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot controller sqlite integracao | Datas UTC | Deve persistir e retornar timestamps em UTC")]
    public async Task Endpoints_ShouldPersistAndReturnUtcDates()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var repository = new ChatbotConversationRepository(context);
            var service = new TelegramChatbotConversationService(repository);
            var controller = new TelegramChatbotController(service);
            var clientId = Guid.NewGuid();

            await SeedClientAsync(context, clientId);
            controller.ControllerContext = BuildClientControllerContext(clientId);

            var interactionAtLocal = DateTime.SpecifyKind(new DateTime(2026, 3, 3, 9, 30, 0), DateTimeKind.Local);
            var openResult = await controller.OpenSession(new TelegramChatbotOpenSessionRequest
            {
                Channel = "telegram",
                ChannelConversationId = "chat-utc-validation",
                InteractionAtUtc = interactionAtLocal
            });

            var openOk = Assert.IsType<OkObjectResult>(openResult);
            var conversation = Assert.IsType<TelegramChatbotConversationDto>(openOk.Value);
            Assert.Equal(DateTimeKind.Utc, conversation.StartedAtUtc.Kind);

            var sentAtLocal = DateTime.SpecifyKind(new DateTime(2026, 3, 3, 10, 45, 0), DateTimeKind.Local);
            var registerMessageResult = await controller.RegisterMessage(new TelegramChatbotRegisterMessageRequest
            {
                ConversationId = conversation.Id,
                Direction = ChatbotMessageDirection.Incoming,
                Source = "telegram",
                Content = "Meu ar condicionado mostra erro CH26.",
                SentAtUtc = sentAtLocal
            });

            var registerMessageOk = Assert.IsType<OkObjectResult>(registerMessageResult);
            var registeredMessage = Assert.IsType<TelegramChatbotMessageDto>(registerMessageOk.Value);
            Assert.Equal(DateTimeKind.Utc, registeredMessage.SentAtUtc.Kind);

            var persistedConversation = await context.ChatbotConversations
                .AsNoTracking()
                .FirstAsync(c => c.Id == conversation.Id);
            var persistedMessage = await context.ChatbotMessages
                .AsNoTracking()
                .FirstAsync(m => m.ConversationId == conversation.Id);

            var expectedInteractionUtc = interactionAtLocal.ToUniversalTime();
            var expectedMessageUtc = sentAtLocal.ToUniversalTime();

            Assert.Equal(expectedInteractionUtc, DateTime.SpecifyKind(persistedConversation.StartedAtUtc, DateTimeKind.Utc));
            Assert.Equal(expectedMessageUtc, DateTime.SpecifyKind(persistedMessage.SentAtUtc, DateTimeKind.Utc));

            var historyResult = await controller.GetConversationHistory(conversation.Id);
            var historyOk = Assert.IsType<OkObjectResult>(historyResult);
            var history = Assert.IsType<TelegramChatbotConversationHistoryDto>(historyOk.Value);

            Assert.Equal(DateTimeKind.Utc, history.Conversation.LastInteractionAtUtc.Kind);
            Assert.Single(history.Messages);
            Assert.Equal(DateTimeKind.Utc, history.Messages[0].SentAtUtc.Kind);
        }
    }

    /// <summary>
    /// Cenario: cliente autenticado consulta carteira de pedidos no chatbot.
    /// Passos: cria pedidos para dois clientes e executa GET de pedidos com cliente A.
    /// Resultado esperado: retorno contem apenas pedidos do cliente autenticado.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot controller sqlite integracao | Query orders | Deve listar apenas pedidos do cliente autenticado")]
    public async Task GetClientOrders_ShouldReturnOnlyAuthenticatedClientOrders()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var clientA = Guid.NewGuid();
            var clientB = Guid.NewGuid();
            await SeedClientAsync(context, clientA);
            await SeedClientAsync(context, clientB);

            context.ServiceRequests.AddRange(
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientA,
                    Category = ServiceCategory.Plumbing,
                    Status = ServiceRequestStatus.Created,
                    Description = "Vazamento cozinha",
                    AddressStreet = "Rua A",
                    AddressCity = "Santos",
                    AddressZip = "11000000",
                    Latitude = 0,
                    Longitude = 0
                },
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientB,
                    Category = ServiceCategory.Appliances,
                    Status = ServiceRequestStatus.Created,
                    Description = "Ar com defeito",
                    AddressStreet = "Rua B",
                    AddressCity = "Praia Grande",
                    AddressZip = "11704150",
                    Latitude = 0,
                    Longitude = 0
                });

            await context.SaveChangesAsync();

            var controller = BuildControllerWithScheduling(context, clientA);
            var result = await controller.GetClientOrders(take: 5, skip: 0);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<TelegramChatbotOrdersResultDto>(ok.Value);
            Assert.True(payload.Success);
            Assert.Single(payload.Orders);
            Assert.Equal("Santos", payload.Orders[0].City);
        }
    }

    /// <summary>
    /// Cenario: cliente tenta consultar status de pedido de outro cliente.
    /// Passos: cria pedido para cliente B e executa endpoint de status autenticado como cliente A.
    /// Resultado esperado: endpoint retorna Forbidden para bloquear acesso cruzado.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot controller sqlite integracao | Query status | Deve bloquear pedido de outro cliente")]
    public async Task GetOrderStatus_ShouldReturnForbid_WhenRequestBelongsToAnotherClient()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var clientA = Guid.NewGuid();
            var clientB = Guid.NewGuid();
            await SeedClientAsync(context, clientA);
            await SeedClientAsync(context, clientB);

            var requestId = Guid.NewGuid();
            context.ServiceRequests.Add(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientB,
                Category = ServiceCategory.Plumbing,
                Status = ServiceRequestStatus.Scheduled,
                Description = "Teste status",
                AddressStreet = "Rua X",
                AddressCity = "Sao Vicente",
                AddressZip = "11300000",
                Latitude = 0,
                Longitude = 0
            });

            await context.SaveChangesAsync();

            var controller = BuildControllerWithScheduling(context, clientA);
            var result = await controller.GetOrderStatus(requestId);

            Assert.IsType<ForbidResult>(result);
        }
    }

    /// <summary>
    /// Cenario: cliente consulta agenda no chatbot com registros de mais de um cliente.
    /// Passos: cria agendamento para cliente A e cliente B, e consulta endpoint autenticado como cliente A.
    /// Resultado esperado: retorno contem somente visitas do cliente autenticado.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot controller sqlite integracao | Query appointments | Deve listar apenas agenda do cliente autenticado")]
    public async Task GetClientAppointments_ShouldReturnOnlyAuthenticatedClientAppointments()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var clientA = Guid.NewGuid();
            var clientB = Guid.NewGuid();
            var providerId = Guid.NewGuid();
            await SeedClientAsync(context, clientA);
            await SeedClientAsync(context, clientB);
            await SeedProviderAsync(context, providerId);

            var requestA = new ServiceRequest
            {
                Id = Guid.NewGuid(),
                ClientId = clientA,
                Category = ServiceCategory.Plumbing,
                Status = ServiceRequestStatus.Scheduled,
                Description = "Torneira",
                AddressStreet = "Rua A",
                AddressCity = "Santos",
                AddressZip = "11000000",
                Latitude = 0,
                Longitude = 0
            };

            var requestB = new ServiceRequest
            {
                Id = Guid.NewGuid(),
                ClientId = clientB,
                Category = ServiceCategory.Plumbing,
                Status = ServiceRequestStatus.Scheduled,
                Description = "Chuveiro",
                AddressStreet = "Rua B",
                AddressCity = "Praia Grande",
                AddressZip = "11704150",
                Latitude = 0,
                Longitude = 0
            };

            context.ServiceRequests.AddRange(requestA, requestB);

            context.ServiceAppointments.AddRange(
                new ServiceAppointment
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestA.Id,
                    ClientId = clientA,
                    ProviderId = providerId,
                    Status = ServiceAppointmentStatus.Confirmed,
                    WindowStartUtc = DateTime.UtcNow.AddDays(1),
                    WindowEndUtc = DateTime.UtcNow.AddDays(1).AddHours(2)
                },
                new ServiceAppointment
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestB.Id,
                    ClientId = clientB,
                    ProviderId = providerId,
                    Status = ServiceAppointmentStatus.Confirmed,
                    WindowStartUtc = DateTime.UtcNow.AddDays(2),
                    WindowEndUtc = DateTime.UtcNow.AddDays(2).AddHours(2)
                });

            await context.SaveChangesAsync();

            var controller = BuildControllerWithScheduling(context, clientA);
            var result = await controller.GetClientAppointments(take: 5, skip: 0);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<TelegramChatbotAppointmentsResultDto>(ok.Value);
            Assert.True(payload.Success);
            Assert.Single(payload.Appointments);
            Assert.Equal(requestA.Id, payload.Appointments[0].ServiceRequestId);
        }
    }

    /// <summary>
    /// Cenario: cliente agenda visita em lote e o fluxo deve sincronizar create no Google Calendar.
    /// Passos: prepara pedido/prestador elegiveis, executa endpoint batch e usa fake do Google para create.
    /// Resultado esperado: agendamento criado, fake chamado uma vez e trilha `ServiceAppointmentCalendarSync` persistida como Synced.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot controller sqlite integracao | Schedule batch | Deve criar sync synced quando Google create succeed")]
    public async Task ScheduleVisitsBatch_ShouldPersistCalendarSyncAsSynced_WhenGoogleCreateSucceeds()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var clientId = Guid.NewGuid();
            var providerId = Guid.NewGuid();
            var requestId = Guid.NewGuid();
            await SeedClientAsync(context, clientId);
            await SeedProviderAsync(context, providerId);

            var serviceRequest = new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Category = ServiceCategory.Plumbing,
                Status = ServiceRequestStatus.Created,
                Description = "Vazamento na cozinha",
                AddressStreet = "Rua Integracao 100",
                AddressCity = "Praia Grande",
                AddressZip = "11704150",
                Latitude = -24.01,
                Longitude = -46.41
            };
            context.ServiceRequests.Add(serviceRequest);

            var providerProfile = new ProviderProfile
            {
                UserId = providerId,
                BaseLatitude = -24.011,
                BaseLongitude = -46.412,
                RadiusKm = 10,
                Categories = [ServiceCategory.Plumbing]
            };
            context.ProviderProfiles.Add(providerProfile);

            var windowStartUtc = DateTime.UtcNow.Date.AddDays(3).AddHours(14);
            var windowEndUtc = windowStartUtc.AddHours(1);
            context.ProviderAvailabilityRules.Add(new ProviderAvailabilityRule
            {
                ProviderId = providerId,
                DayOfWeek = windowStartUtc.DayOfWeek,
                StartTime = TimeSpan.FromHours(8),
                EndTime = TimeSpan.FromHours(22),
                SlotDurationMinutes = 30,
                IsActive = true
            });

            await context.SaveChangesAsync();

            var googleCalendarServiceMock = new Mock<IGoogleCalendarService>();
            googleCalendarServiceMock
                .Setup(service => service.CreateEventAsync(
                    It.IsAny<GoogleCalendarUpsertRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GoogleCalendarUpsertResult(
                    Success: true,
                    EventId: "evt-int-create-001"));

            var appointmentService = BuildRealAppointmentService(context, googleCalendarServiceMock.Object);
            var schedulingService = new TelegramChatbotSchedulingService(
                new ServiceRequestRepository(context),
                new UserRepository(context),
                new ProposalRepository(context),
                new ServiceAppointmentRepository(context),
                new ServiceAppointmentCalendarSyncRepository(context),
                appointmentService,
                googleCalendarServiceMock.Object,
                NullLogger<TelegramChatbotSchedulingService>.Instance);

            var conversationRepository = new ChatbotConversationRepository(context);
            var conversationService = new TelegramChatbotConversationService(conversationRepository);

            var controller = new TelegramChatbotController(conversationService, schedulingService)
            {
                ControllerContext = BuildClientControllerContext(clientId)
            };

            var response = await controller.ScheduleVisitsBatch(
                requestId,
                new TelegramChatbotBatchScheduleVisitsRequest
                {
                    Visits =
                    [
                        new TelegramChatbotBatchScheduleVisitRequestItem
                        {
                            ProviderId = providerId,
                            WindowStartUtc = windowStartUtc,
                            WindowEndUtc = windowEndUtc,
                            Reason = "Agendamento via chatbot"
                        }
                    ]
                });

            var ok = Assert.IsType<OkObjectResult>(response);
            var payload = Assert.IsType<TelegramChatbotBatchScheduleResultDto>(ok.Value);
            Assert.True(payload.Success);
            Assert.Single(payload.Results);
            Assert.True(payload.Results[0].Success);
            Assert.NotNull(payload.Results[0].AppointmentId);

            googleCalendarServiceMock.Verify(
                service => service.CreateEventAsync(
                    It.Is<GoogleCalendarUpsertRequest>(request =>
                        request.IdempotencyKey != null &&
                        request.Description != null &&
                        request.Description.Contains("Protocolo:", StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            var appointmentId = payload.Results[0].AppointmentId!.Value;
            var sync = await context.ServiceAppointmentCalendarSyncs
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.AppointmentId == appointmentId);

            Assert.NotNull(sync);
            Assert.Equal(ServiceAppointmentCalendarSyncStatus.Synced, sync!.SyncStatus);
            Assert.Equal("evt-int-create-001", sync.GoogleEventId);
            Assert.NotNull(sync.LastSyncAtUtc);
            Assert.Null(sync.Error);
        }
    }

    private static ControllerContext BuildClientControllerContext(Guid clientId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, clientId.ToString()),
                new Claim(ClaimTypes.Role, "Client")
            },
            authenticationType: "TestAuth");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private static ServiceAppointmentService BuildRealAppointmentService(
        ConsertaPraMim.Infrastructure.Data.ConsertaPraMimDbContext context,
        IGoogleCalendarService googleCalendarService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:ConfirmationExpiryHours"] = "12",
                ["ServiceAppointments:CancelMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMaximumAdvanceDays"] = "30",
                ["ServiceAppointments:AvailabilityTimeZoneId"] = "UTC"
            })
            .Build();

        return new ServiceAppointmentService(
            new ServiceAppointmentRepository(context),
            new ServiceRequestRepository(context),
            new UserRepository(context),
            new NoOpNotificationService(),
            configuration,
            serviceAppointmentCalendarSyncRepository: new ServiceAppointmentCalendarSyncRepository(context),
            googleCalendarService: googleCalendarService);
    }

    private static TelegramChatbotController BuildControllerWithScheduling(
        ConsertaPraMim.Infrastructure.Data.ConsertaPraMimDbContext context,
        Guid clientId)
    {
        var conversationRepository = new ChatbotConversationRepository(context);
        var conversationService = new TelegramChatbotConversationService(conversationRepository);
        var googleCalendarServiceMock = new Mock<IGoogleCalendarService>();
        googleCalendarServiceMock
            .Setup(service => service.CreateEventAsync(
                It.IsAny<GoogleCalendarUpsertRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleCalendarUpsertResult(
                Success: true,
                EventId: $"cpm-int-{Guid.NewGuid():N}"));

        var schedulingService = new TelegramChatbotSchedulingService(
            new ServiceRequestRepository(context),
            new UserRepository(context),
            new ProposalRepository(context),
            new ServiceAppointmentRepository(context),
            new ServiceAppointmentCalendarSyncRepository(context),
            new Mock<IServiceAppointmentService>().Object,
            googleCalendarServiceMock.Object,
            NullLogger<TelegramChatbotSchedulingService>.Instance);

        var controller = new TelegramChatbotController(conversationService, schedulingService)
        {
            ControllerContext = BuildClientControllerContext(clientId)
        };

        return controller;
    }

    private static async Task SeedClientAsync(ConsertaPraMim.Infrastructure.Data.ConsertaPraMimDbContext context, Guid clientId)
    {
        context.Users.Add(new User
        {
            Id = clientId,
            Name = "Cliente de teste chatbot",
            Email = $"cliente-chatbot-{clientId:N}@consertapramim.test",
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = UserRole.Client,
            IsActive = true
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedProviderAsync(
        ConsertaPraMim.Infrastructure.Data.ConsertaPraMimDbContext context,
        Guid providerId)
    {
        context.Users.Add(new User
        {
            Id = providerId,
            Name = "Prestador chatbot",
            Email = $"prestador-chatbot-{providerId:N}@consertapramim.test",
            PasswordHash = "hash",
            Phone = "11888888888",
            Role = UserRole.Provider,
            IsActive = true
        });

        await context.SaveChangesAsync();
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
        {
            return SendNotificationAsync(recipient, subject, message, actionUrl, data: null);
        }

        public Task SendNotificationAsync(
            string recipient,
            string subject,
            string message,
            string? actionUrl,
            IReadOnlyDictionary<string, string>? data)
        {
            return Task.CompletedTask;
        }
    }
}
