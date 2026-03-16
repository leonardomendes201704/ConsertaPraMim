using System.Text.Json;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderDispatchService : IJourneyProviderDispatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAdminKanbanService _kanbanService;
    private readonly IJourneyGovernanceService _journeyGovernanceService;
    private readonly IJourneyProviderDispatchNotificationService _notificationService;
    private readonly JourneyProviderDispatchOptions _options;
    private readonly ILogger<JourneyProviderDispatchService> _logger;
    private readonly string _workerInstance;

    public JourneyProviderDispatchService(
        IAdminKanbanService kanbanService,
        IJourneyGovernanceService journeyGovernanceService,
        IJourneyProviderDispatchNotificationService notificationService,
        IOptions<JourneyProviderDispatchOptions> options,
        ILogger<JourneyProviderDispatchService> logger)
    {
        _kanbanService = kanbanService;
        _journeyGovernanceService = journeyGovernanceService;
        _notificationService = notificationService;
        _options = options.Value;
        _logger = logger;
        _workerInstance = $"{Environment.MachineName}-journey-dispatch-{Guid.NewGuid():N}";
    }

    public Task<JourneyProviderDispatchRunResult> RunOnceAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(new JourneyProviderDispatchRunResult());
        }

        var governanceDecision = _journeyGovernanceService.EvaluateStep(
            JourneyGovernanceSteps.Dispatch,
            AdminKanbanJourneySourceChannels.Landing);
        if (!governanceDecision.Allowed)
        {
            _logger.LogInformation("JourneyProviderDispatchService ignorado pela governanca. Motivo={Reason}.", governanceDecision.Reason);
            return Task.FromResult(new JourneyProviderDispatchRunResult());
        }

        var normalizedNowUtc = NormalizeUtc(nowUtc) ?? DateTime.UtcNow;
        var readyCount = QueueReadyJourneys(normalizedNowUtc, cancellationToken);
        var queueProcessedCount = ProcessQueue(normalizedNowUtc, cancellationToken);
        var expirationResult = ExpireDueWaves(normalizedNowUtc, cancellationToken);

        var result = new JourneyProviderDispatchRunResult
        {
            ReadyCount = readyCount,
            WavesQueuedCount = readyCount + expirationResult.WavesQueuedCount,
            QueueProcessedCount = queueProcessedCount,
            ExpiredWavesCount = expirationResult.ExpiredWavesCount,
            ExhaustedJourneysCount = expirationResult.ExhaustedJourneysCount
        };

        _logger.LogInformation(
            "JourneyProviderDispatchService processou jornada(s). Ready={ReadyCount} QueueProcessed={QueueProcessedCount} WavesQueued={WavesQueuedCount} Expired={ExpiredWavesCount} Exhausted={ExhaustedJourneysCount}.",
            result.ReadyCount,
            result.QueueProcessedCount,
            result.WavesQueuedCount,
            result.ExpiredWavesCount,
            result.ExhaustedJourneysCount);

        return Task.FromResult(result);
    }

    private int QueueReadyJourneys(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var candidates = _kanbanService
            .ListJourneyStageAutomationCandidates(AdminKanbanBoardTypes.Clients, nowUtc, _options.WorkerBatchSize)
            .Where(item =>
                string.Equals(item.BoardType, AdminKanbanBoardTypes.Clients, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.CurrentState, AdminKanbanJourneyStates.MatchingInProgress, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var queuedCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var journey = _kanbanService.GetJourneyDetails(candidate.LeadId);
            if (journey is null || !CanQueueWave(journey))
            {
                continue;
            }

            if (TryQueueNextWave(journey, nowUtc, "jornada_disparo_onda_criada", out _))
            {
                queuedCount++;
            }
        }

        return queuedCount;
    }

    private int ProcessQueue(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var items = _kanbanService.AcquireDueJourneyDispatchQueueItems(_options.QueueBatchSize, nowUtc, _workerInstance);
        var processedCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (ProcessQueueItem(item, nowUtc, cancellationToken))
                {
                    processedCount++;
                }
            }
            catch (Exception ex)
            {
                var finalStatus = item.AttemptCount >= item.MaxAttempts
                    ? AdminKanbanJourneyDispatchQueueStatuses.DeadLetter
                    : AdminKanbanJourneyDispatchQueueStatuses.Retrying;
                var nextAttemptAt = finalStatus == AdminKanbanJourneyDispatchQueueStatuses.Retrying
                    ? nowUtc.Add(ResolveRetryDelay(item.AttemptCount))
                    : (DateTime?)null;

                _ = _kanbanService.FinalizeJourneyDispatchQueueItem(new AdminKanbanJourneyDispatchQueueFinalizeRequest
                {
                    QueueItemId = item.Id,
                    FinalStatus = finalStatus,
                    FinalizedAt = nowUtc,
                    NextAttemptAt = nextAttemptAt,
                    LastError = ex.Message,
                    WorkerInstance = _workerInstance
                });

                _logger.LogError(
                    ex,
                    "Erro ao processar item da fila de disparo. QueueItemId={QueueItemId} LeadId={LeadId} WaveNumber={WaveNumber}.",
                    item.Id,
                    item.LeadId,
                    item.WaveNumber);
            }
        }

        return processedCount;
    }

    private (int WavesQueuedCount, int ExpiredWavesCount, int ExhaustedJourneysCount) ExpireDueWaves(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var candidates = _kanbanService
            .ListJourneyStageAutomationCandidates(AdminKanbanBoardTypes.Clients, nowUtc, _options.WorkerBatchSize)
            .Where(item =>
                string.Equals(item.BoardType, AdminKanbanBoardTypes.Clients, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.CurrentState, AdminKanbanJourneyStates.WaitingProviderAcceptance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.DispatchStatus, AdminKanbanJourneyDispatchStatuses.WaitingAcceptance, StringComparison.OrdinalIgnoreCase) &&
                item.DispatchWaitingAcceptanceUntilUtc.HasValue &&
                item.DispatchWaitingAcceptanceUntilUtc.Value <= nowUtc &&
                !item.DispatchReservedProviderId.HasValue)
            .ToList();

        var wavesQueuedCount = 0;
        var expiredCount = 0;
        var exhaustedCount = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var journey = _kanbanService.GetJourneyDetails(candidate.LeadId);
            if (journey is null ||
                !string.Equals(journey.Dispatch.Status, AdminKanbanJourneyDispatchStatuses.WaitingAcceptance, StringComparison.OrdinalIgnoreCase) ||
                journey.Dispatch.ReservedProviderId.HasValue)
            {
                continue;
            }

            expiredCount++;
            var expiredDispatch = ExpireCurrentWave(journey.Dispatch, nowUtc);
            var expiredJourney = CloneJourneyWithDispatch(journey, expiredDispatch);

            if (CanQueueWave(expiredJourney))
            {
                if (TryQueueNextWave(expiredJourney, nowUtc, "jornada_disparo_onda_expirada", out _))
                {
                    wavesQueuedCount++;
                }

                continue;
            }

            var exhaustedDispatch = BuildDispatchRecord(
                expiredDispatch,
                AdminKanbanJourneyDispatchStatuses.Exhausted,
                "Todas as ondas de disparo expiraram sem aceite valido de prestador.",
                waitingAcceptanceUntilUtc: null);

            _ = _kanbanService.UpdateJourneyDispatch(journey.LeadId, BuildDispatchUpdateRequest(
                journey,
                exhaustedDispatch,
                AdminKanbanJourneyStates.NoMatch,
                string.Empty,
                string.Empty));

            _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
            {
                LeadId = journey.LeadId,
                BoardType = journey.BoardType,
                TargetStageName = AdminKanbanJourneyClientStageNames.NoMatch,
                TargetCurrentState = AdminKanbanJourneyStates.NoMatch,
                Reason = "Todas as ondas de disparo expiraram sem aceite valido.",
                Origin = AdminKanbanJourneyAutomationOrigins.DispatchEngine,
                HistoryEventType = "jornada_disparo_esgotado",
                HistoryDescription = "A jornada foi marcada como sem match porque nenhuma onda obteve aceite valido.",
                MetadataJson = BuildDispatchMetadataJson(journey, exhaustedDispatch),
                ActiveTimerCode = string.Empty,
                ActiveTimerDueAtUtc = null
            });

            exhaustedCount++;
        }

        return (wavesQueuedCount, expiredCount, exhaustedCount);
    }

    private bool ProcessQueueItem(AdminKanbanJourneyDispatchQueueItemRecord item, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var journey = _kanbanService.GetJourneyDetails(item.LeadId);
        if (journey is null)
        {
            _ = _kanbanService.FinalizeJourneyDispatchQueueItem(new AdminKanbanJourneyDispatchQueueFinalizeRequest
            {
                QueueItemId = item.Id,
                FinalStatus = AdminKanbanJourneyDispatchQueueStatuses.DeadLetter,
                FinalizedAt = nowUtc,
                LastError = "Jornada nao encontrada para o item da fila.",
                WorkerInstance = _workerInstance
            });
            return false;
        }

        var target = journey.Dispatch.Targets.FirstOrDefault(entry => string.Equals(entry.TargetKey, item.TargetKey, StringComparison.Ordinal));
        if (target is null)
        {
            _ = _kanbanService.FinalizeJourneyDispatchQueueItem(new AdminKanbanJourneyDispatchQueueFinalizeRequest
            {
                QueueItemId = item.Id,
                FinalStatus = AdminKanbanJourneyDispatchQueueStatuses.DeadLetter,
                FinalizedAt = nowUtc,
                LastError = "Alvo do disparo nao encontrado no snapshot da jornada.",
                WorkerInstance = _workerInstance
            });
            return false;
        }

        var updatedDispatch = journey.Dispatch;
        if (journey.Dispatch.ReservedProviderId.HasValue &&
            journey.Dispatch.ReservedProviderId.Value != target.ProviderId &&
            string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase))
        {
            updatedDispatch = BuildDispatchRecord(
                updatedDispatch with
                {
                    Targets = updatedDispatch.Targets
                        .Select(entry => string.Equals(entry.TargetKey, target.TargetKey, StringComparison.Ordinal)
                            ? entry with
                            {
                                Status = AdminKanbanJourneyDispatchTargetStatuses.Dispensed,
                                RespondedAtUtc = nowUtc,
                                Note = "Dispensado porque o caso ja foi reservado por outro prestador."
                            }
                            : entry)
                        .ToList()
                },
                updatedDispatch.Status,
                updatedDispatch.Summary,
                updatedDispatch.WaitingAcceptanceUntilUtc);

            PersistDispatchAndFinalizeQueue(journey, item, updatedDispatch, nowUtc, null, false);
            return true;
        }

        if (!string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase))
        {
            _ = _kanbanService.FinalizeJourneyDispatchQueueItem(new AdminKanbanJourneyDispatchQueueFinalizeRequest
            {
                QueueItemId = item.Id,
                FinalStatus = AdminKanbanJourneyDispatchQueueStatuses.Processed,
                FinalizedAt = nowUtc,
                ClearLastError = true,
                WorkerInstance = _workerInstance
            });
            return false;
        }

        var activeWave = journey.Dispatch.Waves
            .OrderByDescending(entry => entry.WaveNumber)
            .FirstOrDefault(entry => entry.WaveNumber == item.WaveNumber);
        var waveExpiresAtUtc = activeWave?.ExpiresAtUtc ?? nowUtc.AddMinutes(_options.AcceptanceTimeoutMinutes);
        var waveBecameActive = string.Equals(activeWave?.Status, AdminKanbanJourneyDispatchWaveStatuses.Queued, StringComparison.OrdinalIgnoreCase);
        var lead = _kanbanService.GetLeadDetails(item.LeadId);
        var deliveryResult = lead is null
            ? new JourneyProviderDispatchNotificationResult
            {
                Success = false,
                PermanentFailure = true,
                DeliveryChannel = "email",
                DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Failed,
                Message = "Lead da jornada nao localizado para montar a notificacao."
            }
            : _notificationService.SendOpportunityAsync(
                new JourneyProviderDispatchNotificationRequest
                {
                    Lead = lead,
                    Target = target,
                    NowUtc = nowUtc
                },
                cancellationToken).GetAwaiter().GetResult();

        if (!deliveryResult.Success && !deliveryResult.PermanentFailure)
        {
            throw new InvalidOperationException(deliveryResult.Message);
        }

        if (!deliveryResult.Success && deliveryResult.PermanentFailure)
        {
            updatedDispatch = journey.Dispatch with
            {
                Waves = journey.Dispatch.Waves
                    .Select(entry => entry.WaveNumber == item.WaveNumber
                        ? entry with
                        {
                            Status = AdminKanbanJourneyDispatchWaveStatuses.Active,
                            ActivatedAtUtc = entry.ActivatedAtUtc ?? nowUtc,
                            Summary = $"Onda {entry.WaveNumber} disparada com falha permanente para um dos alvos."
                        }
                        : entry)
                    .ToList(),
                Targets = journey.Dispatch.Targets
                    .Select(entry => string.Equals(entry.TargetKey, item.TargetKey, StringComparison.Ordinal)
                        ? entry with
                        {
                            Status = AdminKanbanJourneyDispatchTargetStatuses.Dispensed,
                            DeliveryChannel = deliveryResult.DeliveryChannel,
                            DeliveryStatus = deliveryResult.DeliveryStatus,
                            DeliveryAttempts = Math.Max(entry.DeliveryAttempts, item.AttemptCount),
                            LastDeliveryAttemptAtUtc = nowUtc,
                            RespondedAtUtc = nowUtc,
                            LastInteractionAtUtc = nowUtc,
                            LastInteractionSource = "dispatch_worker",
                            LastError = deliveryResult.Message,
                            Note = "Falha permanente ao notificar este prestador por e-mail."
                        }
                        : entry)
                    .ToList()
            };

            var pendingTargetsCount = updatedDispatch.Targets.Count(entry =>
                string.Equals(entry.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Status, AdminKanbanJourneyDispatchTargetStatuses.Sent, StringComparison.OrdinalIgnoreCase));
            updatedDispatch = BuildDispatchRecord(
                updatedDispatch,
                AdminKanbanJourneyDispatchStatuses.WaitingAcceptance,
                pendingTargetsCount > 0
                    ? $"Onda {item.WaveNumber} segue em andamento; um dos alvos falhou na entrega permanente."
                    : $"Onda {item.WaveNumber} ficou sem alvos validos para aceite apos falhas permanentes de entrega.",
                waitingAcceptanceUntilUtc: pendingTargetsCount > 0 ? waveExpiresAtUtc : nowUtc);

            PersistDispatchAndFinalizeQueue(
                journey,
                item,
                updatedDispatch,
                nowUtc,
                waveBecameActive
                    ? new AdminKanbanJourneyStageAutomationUpdateRequest
                    {
                        LeadId = journey.LeadId,
                        BoardType = journey.BoardType,
                        TargetStageName = AdminKanbanJourneyClientStageNames.WaitingAcceptance,
                        TargetCurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                        Reason = $"Onda {item.WaveNumber} iniciou, mas houve falha permanente na notificacao de um alvo.",
                        Origin = AdminKanbanJourneyAutomationOrigins.DispatchEngine,
                        HistoryEventType = "jornada_disparo_falha_permanente",
                        HistoryDescription = "A onda entrou em aguardando aceite, mas um prestador foi dispensado por falha permanente de notificacao.",
                        MetadataJson = BuildDispatchMetadataJson(journey, updatedDispatch),
                        ActiveTimerCode = AdminKanbanJourneyTimerCodes.PendingAcceptance,
                        ActiveTimerDueAtUtc = pendingTargetsCount > 0 ? waveExpiresAtUtc : nowUtc
                    }
                    : null,
                waveBecameActive);

            return true;
        }

        updatedDispatch = journey.Dispatch with
        {
            Waves = journey.Dispatch.Waves
                .Select(entry => entry.WaveNumber == item.WaveNumber
                    ? entry with
                    {
                        Status = AdminKanbanJourneyDispatchWaveStatuses.Active,
                        ActivatedAtUtc = entry.ActivatedAtUtc ?? nowUtc,
                        Summary = $"Onda {entry.WaveNumber} disparada para {entry.TargetCount} prestador(es)."
                    }
                    : entry)
                .ToList(),
            Targets = journey.Dispatch.Targets
                .Select(entry => string.Equals(entry.TargetKey, item.TargetKey, StringComparison.Ordinal)
                    ? entry with
                    {
                        Status = AdminKanbanJourneyDispatchTargetStatuses.Sent,
                        SentAtUtc = nowUtc,
                        ExpiresAtUtc = waveExpiresAtUtc,
                        DeliveryChannel = deliveryResult.DeliveryChannel,
                        DeliveryStatus = deliveryResult.DeliveryStatus,
                        DeliveryAttempts = Math.Max(entry.DeliveryAttempts, item.AttemptCount),
                        LastDeliveryAttemptAtUtc = nowUtc,
                        LastError = string.Empty,
                        Note = deliveryResult.Message
                    }
                    : entry)
                .ToList()
        };

        updatedDispatch = BuildDispatchRecord(
            updatedDispatch,
            AdminKanbanJourneyDispatchStatuses.WaitingAcceptance,
            $"Onda {item.WaveNumber} disparada e aguardando aceite dos prestadores notificados.",
            waitingAcceptanceUntilUtc: waveExpiresAtUtc);

        PersistDispatchAndFinalizeQueue(
            journey,
            item,
            updatedDispatch,
            nowUtc,
            waveBecameActive
                ? new AdminKanbanJourneyStageAutomationUpdateRequest
                {
                    LeadId = journey.LeadId,
                    BoardType = journey.BoardType,
                    TargetStageName = AdminKanbanJourneyClientStageNames.WaitingAcceptance,
                    TargetCurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                    Reason = $"Onda {item.WaveNumber} enviada; aguardando aceite dos prestadores elegiveis.",
                    Origin = AdminKanbanJourneyAutomationOrigins.DispatchEngine,
                    HistoryEventType = "jornada_disparo_onda_enviada",
                    HistoryDescription = $"A onda {item.WaveNumber} foi disparada para prestadores elegiveis e agora aguarda aceite.",
                    MetadataJson = BuildDispatchMetadataJson(journey, updatedDispatch),
                    ActiveTimerCode = AdminKanbanJourneyTimerCodes.PendingAcceptance,
                    ActiveTimerDueAtUtc = waveExpiresAtUtc
                }
                : null,
            waveBecameActive);

        return true;
    }

    private void PersistDispatchAndFinalizeQueue(
        AdminKanbanLeadJourneyRecord journey,
        AdminKanbanJourneyDispatchQueueItemRecord item,
        AdminKanbanJourneyDispatchRecord dispatch,
        DateTime nowUtc,
        AdminKanbanJourneyStageAutomationUpdateRequest? stageUpdate,
        bool addHistory)
    {
        _ = _kanbanService.UpdateJourneyDispatch(
            journey.LeadId,
            BuildDispatchUpdateRequest(
                journey,
                dispatch,
                stageUpdate?.TargetCurrentState ?? journey.CurrentState,
                addHistory && stageUpdate is null ? "jornada_disparo_alvo_atualizado" : string.Empty,
                addHistory && stageUpdate is null ? dispatch.Summary : string.Empty));

        if (stageUpdate is not null)
        {
            _ = _kanbanService.ApplyJourneyStageAutomation(stageUpdate);
        }

        _ = _kanbanService.FinalizeJourneyDispatchQueueItem(new AdminKanbanJourneyDispatchQueueFinalizeRequest
        {
            QueueItemId = item.Id,
            FinalStatus = AdminKanbanJourneyDispatchQueueStatuses.Processed,
            FinalizedAt = nowUtc,
            ClearLastError = true,
            WorkerInstance = _workerInstance
        });
    }

    private bool TryQueueNextWave(AdminKanbanLeadJourneyRecord journey, DateTime nowUtc, string historyEventType, out IReadOnlyList<AdminKanbanJourneyDispatchTargetRecord> newTargets)
    {
        newTargets = [];
        var remainingEligible = GetRemainingEligibleCandidates(journey).ToList();
        if (remainingEligible.Count == 0)
        {
            return false;
        }

        var nextWaveNumber = Math.Max(1, journey.Dispatch.Waves.Count == 0 ? 1 : journey.Dispatch.Waves.Max(item => item.WaveNumber) + 1);
        if (nextWaveNumber > _options.MaxWaves)
        {
            return false;
        }

        var waveExpiresAtUtc = nowUtc.AddMinutes(_options.AcceptanceTimeoutMinutes);
        var selectedProviders = remainingEligible
            .Take(_options.WaveSize)
            .ToList();

        newTargets = selectedProviders
            .Select(provider => new AdminKanbanJourneyDispatchTargetRecord
            {
                TargetKey = BuildTargetKey(journey.LeadId, nextWaveNumber, provider.ProviderId),
                ProviderId = provider.ProviderId,
                ProviderName = provider.ProviderName,
                ProviderEmail = provider.ProviderEmail,
                ProviderPhone = provider.ProviderPhone,
                RankPosition = provider.RankPosition,
                WaveNumber = nextWaveNumber,
                Status = AdminKanbanJourneyDispatchTargetStatuses.Queued,
                CreatedAtUtc = nowUtc,
                ExpiresAtUtc = waveExpiresAtUtc,
                Note = $"Prestador reservado para a onda {nextWaveNumber}."
            })
            .ToList();

        var updatedDispatch = BuildDispatchRecord(
            journey.Dispatch with
            {
                Waves = journey.Dispatch.Waves
                    .Concat(
                    [
                        new AdminKanbanJourneyDispatchWaveRecord
                        {
                            WaveNumber = nextWaveNumber,
                            Status = AdminKanbanJourneyDispatchWaveStatuses.Queued,
                            EligibleSnapshotCount = journey.Matching.EligibleProvidersCount,
                            TargetCount = newTargets.Count,
                            CreatedAtUtc = nowUtc,
                            ExpiresAtUtc = waveExpiresAtUtc,
                            Summary = $"Onda {nextWaveNumber} preparada para {newTargets.Count} prestador(es)."
                        }
                    ])
                    .OrderBy(item => item.WaveNumber)
                    .ToList(),
                Targets = journey.Dispatch.Targets
                    .Concat(newTargets)
                    .OrderBy(item => item.WaveNumber)
                    .ThenBy(item => item.RankPosition <= 0 ? int.MaxValue : item.RankPosition)
                    .ToList()
            },
            AdminKanbanJourneyDispatchStatuses.WaveQueued,
            $"Onda {nextWaveNumber} preparada para {newTargets.Count} prestador(es) elegiveis.",
            waitingAcceptanceUntilUtc: null,
            lastWaveQueuedAtUtc: nowUtc);

        _ = _kanbanService.UpdateJourneyDispatch(
            journey.LeadId,
            BuildDispatchUpdateRequest(
                journey,
                updatedDispatch,
                AdminKanbanJourneyStates.DispatchInProgress,
                string.Empty,
                string.Empty));

        foreach (var target in newTargets)
        {
            var payload = new JourneyProviderDispatchQueuePayload
            {
                LeadId = journey.LeadId,
                JourneyId = journey.JourneyId,
                WaveNumber = nextWaveNumber,
                TargetKey = target.TargetKey,
                ProviderId = target.ProviderId,
                ProviderName = target.ProviderName,
                ProviderEmail = target.ProviderEmail,
                ProviderPhone = target.ProviderPhone,
                RequestedCategory = journey.Matching.RequestedCategory
            };

            _ = _kanbanService.EnqueueJourneyDispatchQueueItem(new AdminKanbanJourneyDispatchQueueEnqueueRequest
            {
                LeadId = journey.LeadId,
                JourneyId = journey.JourneyId,
                WaveNumber = nextWaveNumber,
                ProviderId = target.ProviderId,
                TargetKey = target.TargetKey,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                NextAttemptAt = nowUtc,
                MaxAttempts = _options.QueueMaxAttempts,
                LastError = "Oportunidade preparada para disparo em ondas."
            });
        }

        _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
        {
            LeadId = journey.LeadId,
            BoardType = journey.BoardType,
            TargetStageName = AdminKanbanJourneyClientStageNames.DispatchInProgress,
            TargetCurrentState = AdminKanbanJourneyStates.DispatchInProgress,
            Reason = $"Onda {nextWaveNumber} preparada para disparo aos prestadores elegiveis.",
            Origin = AdminKanbanJourneyAutomationOrigins.DispatchEngine,
            HistoryEventType = historyEventType,
            HistoryDescription = $"A jornada preparou a onda {nextWaveNumber} com {newTargets.Count} prestador(es) elegiveis.",
            MetadataJson = BuildDispatchMetadataJson(journey, updatedDispatch),
            ActiveTimerCode = string.Empty,
            ActiveTimerDueAtUtc = null
        });

        return true;
    }

    private bool CanQueueWave(AdminKanbanLeadJourneyRecord journey)
    {
        if (!string.Equals(journey.BoardType, AdminKanbanBoardTypes.Clients, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(journey.Matching.Status, AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(journey.Matching.Status, AdminKanbanJourneyMatchingStatuses.ReadyForDispatch, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (journey.Dispatch.ReservedProviderId.HasValue ||
            string.Equals(journey.Dispatch.Status, AdminKanbanJourneyDispatchStatuses.Reserved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(journey.Dispatch.Status, AdminKanbanJourneyDispatchStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (journey.Dispatch.CurrentWaveNumber >= _options.MaxWaves)
        {
            return false;
        }

        var hasWaveInFlight = journey.Dispatch.Waves.Any(item =>
            (string.Equals(item.Status, AdminKanbanJourneyDispatchWaveStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.Status, AdminKanbanJourneyDispatchWaveStatuses.Active, StringComparison.OrdinalIgnoreCase)) &&
            !item.CompletedAtUtc.HasValue);
        if (hasWaveInFlight)
        {
            return false;
        }

        return GetRemainingEligibleCandidates(journey).Any();
    }

    private IReadOnlyList<AdminKanbanJourneyProviderMatchRecord> GetRemainingEligibleCandidates(AdminKanbanLeadJourneyRecord journey)
    {
        var targetedProviderIds = journey.Dispatch.Targets
            .Select(item => item.ProviderId)
            .ToHashSet();

        return journey.Matching.Candidates
            .Where(item => item.IsEligible && !targetedProviderIds.Contains(item.ProviderId))
            .OrderBy(item => item.RankPosition <= 0 ? int.MaxValue : item.RankPosition)
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.DistanceKm)
            .ToList();
    }

    private AdminKanbanJourneyDispatchRecord ExpireCurrentWave(AdminKanbanJourneyDispatchRecord dispatch, DateTime nowUtc)
    {
        var currentWaveNumber = dispatch.CurrentWaveNumber;
        var updatedDispatch = dispatch with
        {
            Waves = dispatch.Waves
                .Select(item => item.WaveNumber == currentWaveNumber
                    ? item with
                    {
                        Status = AdminKanbanJourneyDispatchWaveStatuses.Expired,
                        CompletedAtUtc = nowUtc,
                        Summary = $"Onda {item.WaveNumber} expirada sem aceite valido."
                    }
                    : item)
                .ToList(),
            Targets = dispatch.Targets
                .Select(item => item.WaveNumber == currentWaveNumber &&
                                (string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Sent, StringComparison.OrdinalIgnoreCase))
                    ? item with
                    {
                        Status = AdminKanbanJourneyDispatchTargetStatuses.Expired,
                        RespondedAtUtc = nowUtc,
                        Note = "A oportunidade expirou sem aceite na janela da onda."
                    }
                    : item)
                .ToList()
        };

        return BuildDispatchRecord(
            updatedDispatch,
            AdminKanbanJourneyDispatchStatuses.WaveQueued,
            $"Onda {currentWaveNumber} expirou sem aceite valido.",
            waitingAcceptanceUntilUtc: null);
    }

    private AdminKanbanJourneyDispatchRecord BuildDispatchRecord(
        AdminKanbanJourneyDispatchRecord dispatch,
        string status,
        string summary,
        DateTime? waitingAcceptanceUntilUtc,
        DateTime? lastWaveQueuedAtUtc = null)
    {
        var normalizedStatus = AdminKanbanJourneyDispatchStatuses.Normalize(status);
        var normalizedWaves = dispatch.Waves
            .OrderBy(item => item.WaveNumber)
            .Select(item => item with { Status = AdminKanbanJourneyDispatchWaveStatuses.Normalize(item.Status) })
            .ToList();
        var normalizedTargets = dispatch.Targets
            .OrderBy(item => item.WaveNumber)
            .ThenBy(item => item.RankPosition <= 0 ? int.MaxValue : item.RankPosition)
            .Select(item => item with { Status = AdminKanbanJourneyDispatchTargetStatuses.Normalize(item.Status) })
            .ToList();

        return new AdminKanbanJourneyDispatchRecord
        {
            Status = normalizedStatus,
            Summary = summary,
            Strategy = string.IsNullOrWhiteSpace(dispatch.Strategy) ? _options.DispatchStrategy : dispatch.Strategy,
            EligibleProvidersCount = Math.Max(dispatch.EligibleProvidersCount, normalizedTargets.Select(item => item.ProviderId).Distinct().Count()),
            TargetsCreatedCount = normalizedTargets.Count,
            CurrentWaveNumber = normalizedWaves.Count == 0 ? 0 : normalizedWaves.Max(item => item.WaveNumber),
            MaxWaveNumber = Math.Max(dispatch.MaxWaveNumber, normalizedWaves.Count == 0 ? 0 : normalizedWaves.Max(item => item.WaveNumber)),
            SentTargetsCount = normalizedTargets.Count(item => string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Sent, StringComparison.OrdinalIgnoreCase)),
            AcceptedTargetsCount = normalizedTargets.Count(item => string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Accepted, StringComparison.OrdinalIgnoreCase)),
            DeclinedTargetsCount = normalizedTargets.Count(item => string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Declined, StringComparison.OrdinalIgnoreCase)),
            ExpiredTargetsCount = normalizedTargets.Count(item => string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Expired, StringComparison.OrdinalIgnoreCase)),
            PendingTargetsCount = normalizedTargets.Count(item =>
                string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Sent, StringComparison.OrdinalIgnoreCase)),
            LastWaveQueuedAtUtc = lastWaveQueuedAtUtc ?? dispatch.LastWaveQueuedAtUtc,
            WaitingAcceptanceUntilUtc = waitingAcceptanceUntilUtc,
            ReservedProviderId = dispatch.ReservedProviderId,
            ReservedProviderName = dispatch.ReservedProviderName,
            ReservedProviderEmail = dispatch.ReservedProviderEmail,
            ReservedProviderPhone = dispatch.ReservedProviderPhone,
            ReservedAtUtc = dispatch.ReservedAtUtc,
            Waves = normalizedWaves,
            Targets = normalizedTargets
        };
    }

    private AdminKanbanJourneyDispatchUpdateRequest BuildDispatchUpdateRequest(
        AdminKanbanLeadJourneyRecord journey,
        AdminKanbanJourneyDispatchRecord dispatch,
        string currentState,
        string historyEventType,
        string historyDescription)
    {
        return new AdminKanbanJourneyDispatchUpdateRequest
        {
            Status = dispatch.Status,
            Summary = dispatch.Summary,
            Strategy = dispatch.Strategy,
            EligibleProvidersCount = dispatch.EligibleProvidersCount,
            TargetsCreatedCount = dispatch.TargetsCreatedCount,
            CurrentWaveNumber = dispatch.CurrentWaveNumber,
            MaxWaveNumber = dispatch.MaxWaveNumber,
            SentTargetsCount = dispatch.SentTargetsCount,
            AcceptedTargetsCount = dispatch.AcceptedTargetsCount,
            DeclinedTargetsCount = dispatch.DeclinedTargetsCount,
            ExpiredTargetsCount = dispatch.ExpiredTargetsCount,
            PendingTargetsCount = dispatch.PendingTargetsCount,
            LastWaveQueuedAtUtc = dispatch.LastWaveQueuedAtUtc,
            WaitingAcceptanceUntilUtc = dispatch.WaitingAcceptanceUntilUtc,
            ReservedProviderId = dispatch.ReservedProviderId,
            ReservedProviderName = dispatch.ReservedProviderName,
            ReservedProviderEmail = dispatch.ReservedProviderEmail,
            ReservedProviderPhone = dispatch.ReservedProviderPhone,
            ReservedAtUtc = dispatch.ReservedAtUtc,
            CurrentState = currentState,
            HistoryEventType = historyEventType,
            HistoryDescription = historyDescription,
            SourceChannel = journey.SourceChannel,
            MetadataJson = BuildDispatchMetadataJson(journey, dispatch),
            Waves = dispatch.Waves,
            Targets = dispatch.Targets
        };
    }

    private static string BuildTargetKey(int leadId, int waveNumber, Guid providerId)
    {
        return $"lead:{leadId}:wave:{waveNumber}:provider:{providerId:N}";
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static AdminKanbanLeadJourneyRecord CloneJourneyWithDispatch(
        AdminKanbanLeadJourneyRecord journey,
        AdminKanbanJourneyDispatchRecord dispatch)
    {
        return new AdminKanbanLeadJourneyRecord
        {
            JourneyId = journey.JourneyId,
            JourneyPublicId = journey.JourneyPublicId,
            LeadId = journey.LeadId,
            BoardType = journey.BoardType,
            SourceChannel = journey.SourceChannel,
            SourceOrigin = journey.SourceOrigin,
            CurrentState = journey.CurrentState,
            LandingLeadId = journey.LandingLeadId,
            ServiceRequestId = journey.ServiceRequestId,
            ClientId = journey.ClientId,
            VisitorId = journey.VisitorId,
            SessionId = journey.SessionId,
            ChatbotConversationId = journey.ChatbotConversationId,
            ChannelConversationId = journey.ChannelConversationId,
            TelegramChatId = journey.TelegramChatId,
            PrimaryPhone = journey.PrimaryPhone,
            PrimaryEmail = journey.PrimaryEmail,
            CreatedAt = journey.CreatedAt,
            LastIntakeAt = journey.LastIntakeAt,
            UpdatedAt = journey.UpdatedAt,
            StageAutomation = journey.StageAutomation,
            Qualification = journey.Qualification,
            Scheduling = journey.Scheduling,
            Matching = journey.Matching,
            Dispatch = dispatch
        };
    }

    private static TimeSpan ResolveRetryDelay(int attemptCount)
    {
        return attemptCount switch
        {
            <= 0 => TimeSpan.FromSeconds(30),
            1 => TimeSpan.FromMinutes(2),
            2 => TimeSpan.FromMinutes(10),
            _ => TimeSpan.FromMinutes(30)
        };
    }

    private static string BuildDispatchMetadataJson(AdminKanbanLeadJourneyRecord journey, AdminKanbanJourneyDispatchRecord dispatch)
    {
        var payload = new
        {
            leadId = journey.LeadId,
            journeyId = journey.JourneyId,
            currentState = journey.CurrentState,
            matchingStatus = journey.Matching.Status,
            dispatchStatus = dispatch.Status,
            dispatchCurrentWave = dispatch.CurrentWaveNumber,
            dispatchTargetsCreated = dispatch.TargetsCreatedCount,
            requestedCategory = journey.Matching.RequestedCategory,
            requestedSubcategory = journey.Matching.RequestedSubcategory,
            sourceChannel = journey.SourceChannel
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
