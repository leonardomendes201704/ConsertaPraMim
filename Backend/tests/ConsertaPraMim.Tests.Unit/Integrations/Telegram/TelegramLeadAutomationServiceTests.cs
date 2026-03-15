using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramLeadAutomationServiceTests
{
    [Fact(DisplayName = "Telegram Lead Automation | Deve criar lead e sincronizar com Chatwoot")]
    public async Task UpsertLeadAsync_DeveCriarLeadESincronizarComChatwoot()
    {
        var chatbotConversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var chatwootLeadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.UpsertTelegramLead(It.Is<AdminKanbanTelegramLeadUpsertRequest>(request =>
                request.BoardType == AdminKanbanBoardTypes.Clients &&
                request.ChatbotConversationId == chatbotConversationId &&
                request.ChannelConversationId == "chat-telegram-5513" &&
                request.TelegramChatId == 5513997114422 &&
                request.ClientId == userId &&
                request.ClientPhone == "+5513997114422" &&
                request.ClientEmail == "cliente@telegram.com" &&
                request.ServiceCategory == "Eletricista" &&
                request.City == "Praia Grande")))
            .Returns(new AdminKanbanTelegramLeadUpsertResult
            {
                LeadId = 81,
                Created = true,
                StageId = 1,
                BoardType = AdminKanbanBoardTypes.Clients,
                ChatbotConversationId = chatbotConversationId
            });
        chatwootLeadSyncService
            .Setup(service => service.SyncLeadAsync(81, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(ChatwootLeadSyncResult.Synced(
                "Lead sincronizado com Chatwoot.",
                101,
                202,
                1));

        var sut = CreateSut(
            kanbanService.Object,
            chatwootLeadSyncService.Object,
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = true,
                SharedSecret = "segredo-compartilhado",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.UpsertLeadAsync(
            new TelegramLeadAutomationRequest
            {
                BoardType = AdminKanbanBoardTypes.Clients,
                ChatbotConversationId = chatbotConversationId,
                ChannelConversationId = "chat-telegram-5513",
                TelegramChatId = 5513997114422,
                UserId = userId,
                UserName = "Ricardo Almeida",
                UserPhone = "+5513997114422",
                UserEmail = "cliente@telegram.com",
                ServiceCategory = "Eletricista",
                PostalCode = "11701-200",
                City = "Praia Grande",
                LastContactAtUtc = new DateTime(2026, 3, 14, 20, 0, 0, DateTimeKind.Utc)
            },
            "segredo-compartilhado");

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.HttpStatusCode);
        Assert.NotNull(result.Payload);
        Assert.Equal(81, result.Payload!.LeadId);
        Assert.True(result.Payload.Created);
        Assert.Equal(AdminKanbanBoardTypes.Clients, result.Payload.BoardType);
        Assert.Equal("synced", result.Payload.ChatwootStatus);
        Assert.Equal(101, result.Payload.ChatwootContactId);
        Assert.Equal(202, result.Payload.ChatwootConversationId);
        Assert.Equal(1, result.Payload.ChatwootInboxId);
        kanbanService.VerifyAll();
        chatwootLeadSyncService.VerifyAll();
    }

    [Fact(DisplayName = "Telegram Lead Automation | Deve rejeitar segredo invalido")]
    public async Task UpsertLeadAsync_DeveFalharQuandoSegredoForInvalido()
    {
        var sut = CreateSut(
            Mock.Of<IAdminKanbanService>(),
            Mock.Of<IChatwootLeadSyncService>(),
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = true,
                SharedSecret = "segredo-correto",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.UpsertLeadAsync(
            new TelegramLeadAutomationRequest
            {
                BoardType = AdminKanbanBoardTypes.Clients,
                ChatbotConversationId = Guid.NewGuid(),
                ChannelConversationId = "chat-telegram-erro",
                TelegramChatId = 5513997000000,
                UserId = Guid.NewGuid()
            },
            "segredo-incorreto");

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.HttpStatusCode);
        Assert.Contains("invalida", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Telegram Lead Automation | Deve bloquear prestadores quando feature flag estiver desligada")]
    public async Task UpsertLeadAsync_DeveFalharQuandoPrestadoresEstiveremDesabilitados()
    {
        var sut = CreateSut(
            Mock.Of<IAdminKanbanService>(),
            Mock.Of<IChatwootLeadSyncService>(),
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = false,
                SharedSecret = "segredo-correto",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.UpsertLeadAsync(
            new TelegramLeadAutomationRequest
            {
                BoardType = AdminKanbanBoardTypes.Providers,
                ChatbotConversationId = Guid.NewGuid(),
                ChannelConversationId = "chat-provider",
                TelegramChatId = 5513988887777,
                UserId = Guid.NewGuid()
            },
            "segredo-correto");

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Contains("prestadores", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TelegramLeadAutomationService CreateSut(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        TelegramAutomationOptions options)
    {
        return new TelegramLeadAutomationService(
            kanbanService,
            chatwootLeadSyncService,
            Options.Create(options),
            NullLogger<TelegramLeadAutomationService>.Instance);
    }
}
