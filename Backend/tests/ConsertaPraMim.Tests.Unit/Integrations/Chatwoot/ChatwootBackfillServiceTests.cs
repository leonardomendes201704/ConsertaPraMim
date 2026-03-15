using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Chatwoot;

public sealed class ChatwootBackfillServiceTests
{
    [Fact(DisplayName = "Dry-run deve listar candidatos sem sincronizar nem alterar checkpoint")]
    public async Task DryRun_DeveListarCandidatosSemSincronizarNemAlterarCheckpoint()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.GetChatwootBackfillCheckpoint("board:clientes"))
            .Returns(new AdminKanbanChatwootBackfillCheckpointRecord
            {
                ScopeKey = "board:clientes",
                LastProcessedLeadId = 10,
                UpdatedAt = DateTime.UtcNow
            });
        kanbanService
            .Setup(service => service.ListChatwootBackfillCandidates(AdminKanbanBoardTypes.Clients, 10, 20))
            .Returns(
            [
                new AdminKanbanChatwootBackfillCandidateRecord
                {
                    LeadId = 11,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageName = "Novo lead",
                    LeadName = "Ricardo Almeida",
                    Phone = "(13) 99711-4422",
                    Email = "ricardo@email.com"
                },
                new AdminKanbanChatwootBackfillCandidateRecord
                {
                    LeadId = 12,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageName = "Novo lead",
                    LeadName = "Lead sem contato",
                    Phone = string.Empty,
                    Email = string.Empty
                }
            ]);

        var sut = CreateSut(kanbanService.Object, leadSyncService.Object);

        var result = await sut.RunAsync(new ChatwootBackfillRunRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            BatchSize = 20,
            DryRun = true
        });

        Assert.True(result.DryRun);
        Assert.Equal(ChatwootBackfillRunStatuses.DryRun, result.Status);
        Assert.Equal(2, result.TotalSelected);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Null(result.LastProcessedLeadId);
        leadSyncService.VerifyNoOtherCalls();
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Dry-run deve considerar lead Telegram sem telefone ou e-mail como elegivel quando houver identificador tecnico")]
    public async Task DryRun_DeveConsiderarLeadTelegramSemContatoComoElegivelQuandoHouverIdentificadorTecnico()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.GetChatwootBackfillCheckpoint("board:clientes"))
            .Returns(new AdminKanbanChatwootBackfillCheckpointRecord
            {
                ScopeKey = "board:clientes",
                LastProcessedLeadId = 12,
                UpdatedAt = DateTime.UtcNow
            });
        kanbanService
            .Setup(service => service.ListChatwootBackfillCandidates(AdminKanbanBoardTypes.Clients, 12, 20))
            .Returns(
            [
                new AdminKanbanChatwootBackfillCandidateRecord
                {
                    LeadId = 13,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageName = "Novo lead",
                    LeadName = "Lead Telegram sem contato",
                    Phone = string.Empty,
                    Email = string.Empty,
                    Source = "Telegram",
                    TelegramChatId = 7788990011,
                    TelegramChannelConversationId = "7788990011"
                }
            ]);

        var sut = CreateSut(kanbanService.Object, leadSyncService.Object);

        var result = await sut.RunAsync(new ChatwootBackfillRunRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            BatchSize = 20,
            DryRun = true
        });

        Assert.True(result.DryRun);
        Assert.Equal(1, result.TotalSelected);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains(result.Items, item => item.Message.Contains("identificador tecnico do bot", StringComparison.OrdinalIgnoreCase));
        leadSyncService.VerifyNoOtherCalls();
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Execucao real deve respeitar checkpoint salvo e avancar cursor apos processar lote")]
    public async Task ExecucaoReal_DeveRespeitarCheckpointSalvoEAvancarCursorAposProcessarLote()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var initialCheckpoint = new AdminKanbanChatwootBackfillCheckpointRecord
        {
            ScopeKey = "board:clientes",
            LastProcessedLeadId = 10,
            UpdatedAt = DateTime.UtcNow
        };
        AdminKanbanChatwootBackfillCheckpointUpsertRequest? lastCheckpointRequest = null;

        kanbanService
            .Setup(service => service.GetChatwootBackfillCheckpoint("board:clientes"))
            .Returns(initialCheckpoint);
        kanbanService
            .Setup(service => service.ListChatwootBackfillCandidates(AdminKanbanBoardTypes.Clients, 10, 20))
            .Returns(
            [
                new AdminKanbanChatwootBackfillCandidateRecord
                {
                    LeadId = 11,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageName = "Novo lead",
                    LeadName = "Lead 11",
                    Phone = "(13) 99711-4422",
                    Email = "lead11@email.com"
                },
                new AdminKanbanChatwootBackfillCandidateRecord
                {
                    LeadId = 12,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageName = "Agendado",
                    LeadName = "Lead 12",
                    Phone = "(13) 99655-8822",
                    Email = "lead12@email.com"
                }
            ]);
        kanbanService
            .Setup(service => service.SaveChatwootBackfillCheckpoint(It.IsAny<AdminKanbanChatwootBackfillCheckpointUpsertRequest>()))
            .Callback<AdminKanbanChatwootBackfillCheckpointUpsertRequest>(request => lastCheckpointRequest = request)
            .Returns<AdminKanbanChatwootBackfillCheckpointUpsertRequest>(request => new AdminKanbanChatwootBackfillCheckpointRecord
            {
                ScopeKey = request.ScopeKey,
                LastProcessedLeadId = request.LastProcessedLeadId,
                LastRunStartedAt = request.LastRunStartedAt,
                LastRunCompletedAt = request.LastRunCompletedAt,
                LastRunStatus = request.LastRunStatus,
                LastSummaryJson = request.LastSummaryJson,
                UpdatedAt = DateTime.UtcNow
            });

        leadSyncService
            .Setup(service => service.SyncLeadAsync(11, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(ChatwootLeadSyncResult.Synced("Lead 11 sincronizado.", 101, 201, 1));
        leadSyncService
            .Setup(service => service.SyncLeadAsync(12, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(ChatwootLeadSyncResult.Failed("Falha externa com retentativa.", 102, null, 1, retrySuggested: true, queuedForRetry: true));

        var sut = CreateSut(kanbanService.Object, leadSyncService.Object);

        var result = await sut.RunAsync(new ChatwootBackfillRunRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            BatchSize = 20,
            DryRun = false
        });

        Assert.False(result.DryRun);
        Assert.Equal(ChatwootBackfillRunStatuses.Completed, result.Status);
        Assert.Equal(2, result.TotalSelected);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(12, result.LastProcessedLeadId);
        Assert.NotNull(lastCheckpointRequest);
        Assert.Equal(12, lastCheckpointRequest!.LastProcessedLeadId);
        Assert.Equal(ChatwootBackfillRunStatuses.Completed, lastCheckpointRequest.LastRunStatus);
        leadSyncService.VerifyAll();
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Execucao real deve permitir override do checkpoint com Lead ID inicial informado")]
    public async Task ExecucaoReal_DevePermitirOverrideDoCheckpointComLeadIdInicialInformado()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.GetChatwootBackfillCheckpoint("board:prestadores"))
            .Returns(new AdminKanbanChatwootBackfillCheckpointRecord
            {
                ScopeKey = "board:prestadores",
                LastProcessedLeadId = 25,
                UpdatedAt = DateTime.UtcNow
            });
        kanbanService
            .Setup(service => service.ListChatwootBackfillCandidates(AdminKanbanBoardTypes.Providers, 99, 5))
            .Returns([]);
        kanbanService
            .Setup(service => service.SaveChatwootBackfillCheckpoint(It.IsAny<AdminKanbanChatwootBackfillCheckpointUpsertRequest>()))
            .Returns<AdminKanbanChatwootBackfillCheckpointUpsertRequest>(request => new AdminKanbanChatwootBackfillCheckpointRecord
            {
                ScopeKey = request.ScopeKey,
                LastProcessedLeadId = request.LastProcessedLeadId,
                LastRunStartedAt = request.LastRunStartedAt,
                LastRunCompletedAt = request.LastRunCompletedAt,
                LastRunStatus = request.LastRunStatus,
                LastSummaryJson = request.LastSummaryJson,
                UpdatedAt = DateTime.UtcNow
            });

        var sut = CreateSut(kanbanService.Object, leadSyncService.Object);

        var result = await sut.RunAsync(new ChatwootBackfillRunRequest
        {
            BoardType = AdminKanbanBoardTypes.Providers,
            BatchSize = 5,
            DryRun = false,
            StartAfterLeadId = 99
        });

        Assert.Equal(99, result.EffectiveStartAfterLeadId);
        Assert.Equal(0, result.TotalSelected);
        leadSyncService.VerifyNoOtherCalls();
        kanbanService.VerifyAll();
    }

    private static ChatwootBackfillService CreateSut(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService leadSyncService,
        bool enabled = true)
    {
        var options = Options.Create(new ChatwootOptions
        {
            Enabled = enabled,
            BaseUrl = "https://chatwoot.exemplo.com",
            ApiAccessToken = "token",
            AccountId = 1,
            ClientsInboxId = 1,
            ProvidersInboxId = 2,
            WebhookSecret = "secret"
        });

        return new ChatwootBackfillService(
            kanbanService,
            leadSyncService,
            options,
            NullLogger<ChatwootBackfillService>.Instance);
    }
}
