using System.Reflection;
using AppMobileCPM.Areas.Admin.Controllers;
using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public sealed class KanbanControllerTests
{
    [Fact(DisplayName = "KanbanController | Deve excluir contato no Chatwoot quando checkbox estiver marcado")]
    public async Task DeleteLead_DeveExcluirContatoNoChatwoot_QuandoCheckboxMarcado()
    {
        var kanbanServiceMock = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var chatwootApiClientMock = new Mock<IChatwootApiClient>(MockBehavior.Strict);
        var telegramBridgeClientMock = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);

        const int leadId = 42;
        const long telegramChatId = 5511999999999;
        const long chatwootContactId = 9876;

        kanbanServiceMock
            .Setup(service => service.GetLeadDetails(leadId))
            .Returns(BuildLeadDetails(leadId, telegramChatId, chatwootContactId));
        telegramBridgeClientMock
            .Setup(client => client.ResetHumanHandoffAsync(
                It.Is<TelegramBridgeResetHandoffRequest>(request => request.TelegramChatId == telegramChatId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramBridgeResetHandoffResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                Message = "Handoff resetado."
            });
        chatwootApiClientMock
            .Setup(client => client.DeleteContactAsync(chatwootContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatwootDeleteContactResult.DeletedResult("Contato excluido no Chatwoot com sucesso."));
        kanbanServiceMock
            .Setup(service => service.DeleteLead(leadId))
            .Returns(true);

        var controller = CreateController(
            kanbanServiceMock.Object,
            chatwootApiClientMock.Object,
            telegramBridgeClientMock.Object,
            new ChatwootOptions
            {
                Enabled = true,
                BaseUrl = "https://chatwoot.consertapramim.com",
                ApiAccessToken = "token",
                AccountId = 1
            },
            new TelegramAutomationOptions
            {
                Enabled = true,
                TelegramBridgeBaseUrl = "https://telegram.consertapramim.com",
                SharedSecret = "segredo"
            });

        var result = await controller.DeleteLead(leadId, new AdminKanbanLeadDeleteInputModel
        {
            DeleteChatwootContact = true
        });

        var json = Assert.IsType<JsonResult>(result);
        Assert.True(GetAnonymousProperty<bool>(json.Value!, "success"));
        Assert.True(GetAnonymousProperty<bool>(json.Value!, "telegramHandoffReset"));
        Assert.True(GetAnonymousProperty<bool>(json.Value!, "chatwootContactDeleted"));
        Assert.Contains("Contato do Chatwoot excluido com sucesso.", GetAnonymousProperty<string>(json.Value!, "message"));

        chatwootApiClientMock.Verify(client => client.DeleteContactAsync(chatwootContactId, It.IsAny<CancellationToken>()), Times.Once);
        kanbanServiceMock.Verify(service => service.DeleteLead(leadId), Times.Once);
    }

    [Fact(DisplayName = "KanbanController | Nao deve excluir lead local quando delecao do contato no Chatwoot falhar")]
    public async Task DeleteLead_NaoDeveExcluirLeadLocal_QuandoDelecaoDoContatoNoChatwootFalhar()
    {
        var kanbanServiceMock = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var chatwootApiClientMock = new Mock<IChatwootApiClient>(MockBehavior.Strict);
        var telegramBridgeClientMock = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);

        const int leadId = 43;
        const long chatwootContactId = 555;

        kanbanServiceMock
            .Setup(service => service.GetLeadDetails(leadId))
            .Returns(BuildLeadDetails(leadId, telegramChatId: null, chatwootContactId));
        chatwootApiClientMock
            .Setup(client => client.DeleteContactAsync(chatwootContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatwootDeleteContactResult.Failed(
                StatusCodes.Status502BadGateway,
                "Falha ao acessar o Chatwoot."));

        var controller = CreateController(
            kanbanServiceMock.Object,
            chatwootApiClientMock.Object,
            telegramBridgeClientMock.Object,
            new ChatwootOptions
            {
                Enabled = true,
                BaseUrl = "https://chatwoot.consertapramim.com",
                ApiAccessToken = "token",
                AccountId = 1
            },
            new TelegramAutomationOptions
            {
                Enabled = true,
                TelegramBridgeBaseUrl = "https://telegram.consertapramim.com",
                SharedSecret = "segredo"
            });

        var result = await controller.DeleteLead(leadId, new AdminKanbanLeadDeleteInputModel
        {
            DeleteChatwootContact = true
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = badRequest.Value!;
        Assert.False(GetAnonymousProperty<bool>(payload, "success"));
        Assert.Contains("Nao foi possivel excluir o contato no Chatwoot antes da exclusao local.", GetAnonymousProperty<string>(payload, "message"));

        chatwootApiClientMock.Verify(client => client.DeleteContactAsync(chatwootContactId, It.IsAny<CancellationToken>()), Times.Once);
        kanbanServiceMock.Verify(service => service.DeleteLead(It.IsAny<int>()), Times.Never);
    }

    private static KanbanController CreateController(
        IAdminKanbanService kanbanService,
        IChatwootApiClient chatwootApiClient,
        ITelegramBridgeDeliveryClient telegramBridgeDeliveryClient,
        ChatwootOptions chatwootOptions,
        TelegramAutomationOptions telegramAutomationOptions)
    {
        var controller = new KanbanController(
            kanbanService,
            chatwootApiClient,
            Mock.Of<IChatwootSyncQueueService>(),
            Mock.Of<IChatwootLeadSyncService>(),
            Mock.Of<IChatwootBackfillService>(),
            telegramBridgeDeliveryClient,
            Mock.Of<ITelegramBridgeObservabilityClient>(),
            Options.Create(chatwootOptions),
            Options.Create(telegramAutomationOptions))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static AdminKanbanLeadDetailsRecord BuildLeadDetails(int leadId, long? telegramChatId, long? chatwootContactId) =>
        new()
        {
            Id = leadId,
            StageId = 1,
            StageName = "Novo",
            BoardType = AdminKanbanBoardTypes.Clients,
            Name = "Lead teste",
            History = [],
            Telegram = new AdminKanbanLeadTelegramLinkRecord
            {
                TelegramChatId = telegramChatId
            },
            Chatwoot = new AdminKanbanLeadChatwootSyncRecord
            {
                ContactId = chatwootContactId,
                SyncStatus = ChatwootSyncStatuses.Synced
            }
        };

    private static T GetAnonymousProperty<T>(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        Assert.NotNull(property);
        return Assert.IsType<T>(property!.GetValue(value));
    }
}
