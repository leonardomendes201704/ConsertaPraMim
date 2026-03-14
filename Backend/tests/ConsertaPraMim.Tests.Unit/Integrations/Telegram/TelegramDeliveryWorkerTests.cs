using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramDeliveryWorkerTests
{
    [Fact(DisplayName = "Telegram Delivery Worker | RunOnce | Deve processar item devido com sucesso")]
    public async Task RunOnceAsync_DeveProcessarItemDevidoComSucesso()
    {
        var queueService = new Mock<ITelegramDeliveryQueueService>(MockBehavior.Strict);
        var automationService = new Mock<ITelegramMessageAutomationService>(MockBehavior.Strict);
        var dueItem = new AdminKanbanTelegramDeliveryQueueItemRecord
        {
            Id = 1,
            LeadId = 88,
            Direction = TelegramDeliveryDirections.TelegramToChatwoot,
            DeliveryKey = "telegram:5513997000011:101",
            Status = TelegramDeliveryQueueStatuses.Processing,
            AttemptCount = 1,
            MaxAttempts = 10,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        queueService
            .Setup(service => service.AcquireDueItems(It.IsAny<string>(), 20, It.IsAny<DateTime>()))
            .Returns([dueItem]);
        automationService
            .Setup(service => service.ProcessQueueItemAsync(dueItem, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelegramDeliveryProcessResult.Ok("Mensagem entregue."));
        queueService
            .Setup(service => service.MarkProcessed(dueItem, It.IsAny<string>(), It.Is<string?>(note => note == "Mensagem entregue.")))
            .Returns(TelegramDeliveryQueueStatuses.Processed);

        var worker = CreateWorker(queueService.Object, automationService.Object);

        var processed = await worker.RunOnceAsync();

        Assert.Equal(1, processed);
        queueService.VerifyAll();
        automationService.VerifyAll();
    }

    [Fact(DisplayName = "Telegram Delivery Worker | RunOnce | Deve mandar item para dead-letter quando retentativa esgota")]
    public async Task RunOnceAsync_DeveMandarItemParaDeadLetterQuandoRetentativaEsgota()
    {
        var queueService = new Mock<ITelegramDeliveryQueueService>(MockBehavior.Strict);
        var automationService = new Mock<ITelegramMessageAutomationService>(MockBehavior.Strict);
        var dueItem = new AdminKanbanTelegramDeliveryQueueItemRecord
        {
            Id = 2,
            LeadId = 89,
            Direction = TelegramDeliveryDirections.ChatwootToTelegram,
            DeliveryKey = "chatwoot:998877",
            Status = TelegramDeliveryQueueStatuses.Processing,
            AttemptCount = 10,
            MaxAttempts = 10,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        queueService
            .Setup(service => service.AcquireDueItems(It.IsAny<string>(), 20, It.IsAny<DateTime>()))
            .Returns([dueItem]);
        automationService
            .Setup(service => service.ProcessQueueItemAsync(dueItem, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelegramDeliveryProcessResult.Failed("Bridge indisponivel.", retrySuggested: true));
        queueService
            .Setup(service => service.MarkFailed(
                dueItem,
                It.IsAny<string>(),
                It.Is<string>(message => message.Contains("Bridge", StringComparison.OrdinalIgnoreCase)),
                true))
            .Returns(TelegramDeliveryQueueStatuses.DeadLetter);

        var worker = CreateWorker(queueService.Object, automationService.Object);

        var processed = await worker.RunOnceAsync();

        Assert.Equal(1, processed);
        queueService.VerifyAll();
        automationService.VerifyAll();
    }

    private static TelegramDeliveryWorker CreateWorker(
        ITelegramDeliveryQueueService queueService,
        ITelegramMessageAutomationService automationService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => queueService);
        services.AddScoped(_ => automationService);

        var provider = services.BuildServiceProvider();
        return new TelegramDeliveryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TelegramAutomationOptions
            {
                Enabled = true,
                MirrorMessagesEnabled = true,
                DeliveryWorkerEnabled = true,
                DeliveryWorkerIntervalSeconds = 20,
                DeliveryWorkerBatchSize = 20,
                DeliveryQueueMaxAttempts = 10
            }),
            NullLogger<TelegramDeliveryWorker>.Instance);
    }
}
