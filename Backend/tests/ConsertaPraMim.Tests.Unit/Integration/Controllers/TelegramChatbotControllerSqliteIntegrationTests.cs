using System.Security.Claims;
using ConsertaPraMim.API.Contracts;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
}
