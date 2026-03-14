using ConsertaPraMim.Web.TelegramBridge.Controllers;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public sealed class TelegramWebhookControllerTests
{
    [Fact(DisplayName = "Telegram webhook controller | Deve rejeitar secret token invalido")]
    public async Task Receive_DeveRejeitarSecretTokenInvalido()
    {
        var processor = new Mock<ITelegramInboundUpdateProcessor>(MockBehavior.Strict);
        var controller = CreateController(processor.Object, new TelegramBridgeOptions
        {
            UpdateTransport = TelegramBridgeOptions.WebhookTransport,
            WebhookSecretToken = "segredo-correto"
        });

        controller.ControllerContext.HttpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = "segredo-incorreto";

        var result = await controller.Receive(
            new TelegramUpdate
            {
                UpdateId = 8001,
                Message = new TelegramMessage
                {
                    MessageId = 1,
                    Chat = new TelegramChat { Id = 5513997000000 }
                }
            },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact(DisplayName = "Telegram webhook controller | Deve retornar not found quando modo webhook estiver desligado")]
    public async Task Receive_DeveRetornarNotFoundQuandoModoWebhookEstiverDesligado()
    {
        var processor = new Mock<ITelegramInboundUpdateProcessor>(MockBehavior.Strict);
        var controller = CreateController(processor.Object, new TelegramBridgeOptions
        {
            UpdateTransport = TelegramBridgeOptions.LongPollingTransport,
            WebhookSecretToken = "segredo-correto"
        });

        var result = await controller.Receive(
            new TelegramUpdate
            {
                UpdateId = 8002,
                Message = new TelegramMessage
                {
                    MessageId = 2,
                    Chat = new TelegramChat { Id = 5513997000001 }
                }
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "Telegram webhook controller | Deve processar update valido")]
    public async Task Receive_DeveProcessarUpdateValido()
    {
        var processor = new Mock<ITelegramInboundUpdateProcessor>(MockBehavior.Strict);
        processor
            .Setup(service => service.ProcessAsync(
                It.Is<TelegramUpdate>(update => update.UpdateId == 8003),
                "webhook",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(processor.Object, new TelegramBridgeOptions
        {
            UpdateTransport = TelegramBridgeOptions.WebhookTransport,
            WebhookSecretToken = "segredo-correto"
        });

        controller.ControllerContext.HttpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = "segredo-correto";

        var result = await controller.Receive(
            new TelegramUpdate
            {
                UpdateId = 8003,
                Message = new TelegramMessage
                {
                    MessageId = 3,
                    Chat = new TelegramChat { Id = 5513997000002 }
                }
            },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        processor.VerifyAll();
    }

    private static TelegramWebhookController CreateController(
        ITelegramInboundUpdateProcessor processor,
        TelegramBridgeOptions options)
    {
        return new TelegramWebhookController(processor, Options.Create(options))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
