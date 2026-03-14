using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Chatwoot;

public sealed class ChatwootSyncRetryWorkerTests
{
    [Fact(DisplayName = "Worker de retentativa Chatwoot | RunOnce | Deve processar item devido com sucesso")]
    public async Task RunOnceAsync_DeveProcessarItemDevidoComSucesso()
    {
        var queueService = new Mock<IChatwootSyncQueueService>();
        var leadSyncService = new Mock<IChatwootLeadSyncService>();
        var dueItem = new AdminKanbanChatwootSyncQueueItemRecord
        {
            Id = 1,
            LeadId = 8,
            OperationType = ChatwootSyncOperationTypes.LeadSync,
            Status = ChatwootSyncQueueStatuses.Processing,
            AttemptCount = 1,
            MaxAttempts = 10,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        queueService
            .Setup(service => service.AcquireDueItems(It.IsAny<string>(), 20, It.IsAny<DateTime>()))
            .Returns([dueItem]);
        leadSyncService
            .Setup(service => service.SyncLeadAsync(8, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(ChatwootLeadSyncResult.Synced("Lead sincronizado com Chatwoot.", 1001, 2001, 1));
        queueService
            .Setup(service => service.MarkProcessed(dueItem, It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(ChatwootSyncQueueStatuses.Processed);

        var worker = CreateWorker(queueService.Object, leadSyncService.Object);

        var processed = await worker.RunOnceAsync();

        Assert.Equal(1, processed);
        queueService.VerifyAll();
        leadSyncService.VerifyAll();
    }

    [Fact(DisplayName = "Worker de retentativa Chatwoot | RunOnce | Deve encaminhar falha para dead-letter quando retentativa esgota")]
    public async Task RunOnceAsync_DeveEncaminharFalhaParaDeadLetterQuandoRetentativaEsgota()
    {
        var queueService = new Mock<IChatwootSyncQueueService>();
        var leadSyncService = new Mock<IChatwootLeadSyncService>();
        var dueItem = new AdminKanbanChatwootSyncQueueItemRecord
        {
            Id = 9,
            LeadId = 15,
            OperationType = ChatwootSyncOperationTypes.StageSync,
            Status = ChatwootSyncQueueStatuses.Processing,
            AttemptCount = 10,
            MaxAttempts = 10,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        queueService
            .Setup(service => service.AcquireDueItems(It.IsAny<string>(), 20, It.IsAny<DateTime>()))
            .Returns([dueItem]);
        leadSyncService
            .Setup(service => service.SyncLeadStageAsync(15, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(ChatwootLeadSyncResult.Failed(
                "Falha de rede ao acessar o Chatwoot.",
                1001,
                2001,
                1,
                retrySuggested: true));
        queueService
            .Setup(service => service.MarkFailed(
                dueItem,
                It.IsAny<string>(),
                It.Is<string>(message => message.Contains("rede", StringComparison.OrdinalIgnoreCase)),
                true))
            .Returns(ChatwootSyncQueueStatuses.DeadLetter);

        var worker = CreateWorker(queueService.Object, leadSyncService.Object);

        var processed = await worker.RunOnceAsync();

        Assert.Equal(1, processed);
        queueService.VerifyAll();
        leadSyncService.VerifyAll();
    }

    private static ChatwootSyncRetryWorker CreateWorker(
        IChatwootSyncQueueService queueService,
        IChatwootLeadSyncService leadSyncService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => queueService);
        services.AddScoped(_ => leadSyncService);

        var provider = services.BuildServiceProvider();
        return new ChatwootSyncRetryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ChatwootOptions
            {
                Enabled = true,
                RetryWorkerEnabled = true,
                RetryWorkerBatchSize = 20,
                RetryWorkerIntervalSeconds = 30,
                SyncQueueMaxAttempts = 10
            }),
            NullLogger<ChatwootSyncRetryWorker>.Instance);
    }
}
