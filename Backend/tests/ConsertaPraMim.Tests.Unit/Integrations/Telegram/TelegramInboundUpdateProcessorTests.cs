using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramInboundUpdateProcessorTests
{
    [Fact(DisplayName = "Telegram inbound update processor | Deve ignorar update sem mensagem")]
    public async Task ProcessAsync_DeveIgnorarUpdateSemMensagem()
    {
        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        var sut = CreateSut(chatService.Object, automationClient.Object, observability.Object);

        var result = await sut.ProcessAsync(new TelegramUpdate
        {
            UpdateId = 9001,
            Message = null
        }, "webhook", CancellationToken.None);

        Assert.False(result);
    }

    [Fact(DisplayName = "Telegram inbound update processor | Deve persistir update e espelhar quando mirror estiver habilitado")]
    public async Task ProcessAsync_DevePersistirUpdateEEspelharQuandoMirrorEstiverHabilitado()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9002,
            Message = new TelegramMessage
            {
                MessageId = 321,
                DateUnix = 1_773_512_400,
                Text = "Preciso de ajuda com meu chuveiro.",
                Chat = new TelegramChat
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo",
                    LastName = "Almeida"
                },
                From = new TelegramUser
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo",
                    LastName = "Almeida"
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513997114422:321",
            ChatId: 5513997114422,
            IsOutgoing: false,
            SenderDisplayName: "Ricardo Almeida",
            Text: "Preciso de ajuda com meu chuveiro.",
            SentAtUtc: new DateTimeOffset(2026, 3, 14, 18, 0, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        observability
            .Setup(service => service.RecordInboundMessage(0));
        chatService
            .Setup(service => service.ReceiveFromTelegramAsync(update.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedMessage);
        automationClient
            .Setup(client => client.MirrorInboundMessageAsync(
                It.Is<TelegramInboundMessageAutomationRequest>(request =>
                    request.TelegramChatId == 5513997114422 &&
                    request.ChannelConversationId == "5513997114422" &&
                    request.ChannelMessageId == "telegram:5513997114422:321" &&
                    request.MessageText == "Preciso de ajuda com meu chuveiro."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });

        var sut = CreateSut(
            chatService.Object,
            automationClient.Object,
            observability.Object,
            new TelegramAutomationOptions
            {
                Enabled = true,
                MirrorMessagesEnabled = true,
                SharedSecret = "segredo-compartilhado",
                CpmFullBaseUrl = "https://www.consertapramim.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.ProcessAsync(update, "polling", CancellationToken.None);

        Assert.True(result);
        chatService.VerifyAll();
        automationClient.VerifyAll();
        observability.VerifyAll();
    }

    private static TelegramInboundUpdateProcessor CreateSut(
        ITelegramChatService chatService,
        ITelegramMessageAutomationClient automationClient,
        ITelegramChatbotObservabilityService observabilityService,
        TelegramAutomationOptions? options = null)
    {
        return new TelegramInboundUpdateProcessor(
            chatService,
            Options.Create(options ?? new TelegramAutomationOptions
            {
                Enabled = false,
                MirrorMessagesEnabled = false,
                SharedSecret = "segredo-compartilhado",
                CpmFullBaseUrl = "https://www.consertapramim.com",
                RequestTimeoutSeconds = 15
            }),
            automationClient,
            observabilityService,
            NullLogger<TelegramInboundUpdateProcessor>.Instance);
    }
}
