using System.Data;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed partial class SqlAdminKanbanService
{
    public AdminKanbanJourneyDispatchTargetInteractionResult? ApplyJourneyDispatchTargetInteraction(AdminKanbanJourneyDispatchTargetInteractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        JourneyAutomationExecutionMatch? match;
        string dispatchSnapshotJson;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
SELECT TOP (1)
    j.Id,
    j.LeadId,
    j.BoardType,
    j.SourceChannel,
    j.CurrentState,
    lead.StageId,
    stage.Name,
    j.LastStageAutomationReason,
    j.LastStageAutomationOrigin,
    j.ActiveTimerCode,
    j.ActiveTimerDueAtUtc,
    j.DispatchSnapshotJson
FROM dbo.{TablePrefix}journey_executions j WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = j.LeadId
INNER JOIN dbo.{TablePrefix}kanban_stages stage ON stage.Id = lead.StageId
WHERE j.LeadId = @leadId
  AND lead.IsActive = 1
ORDER BY COALESCE(j.UpdatedAt, j.LastIntakeAt, j.CreatedAt) DESC, j.Id DESC;
""";
            command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = request.LeadId });

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            match = new JourneyAutomationExecutionMatch
            {
                JourneyId = reader.GetInt32(0),
                LeadId = reader.GetInt32(1),
                BoardType = reader.GetString(2),
                SourceChannel = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                CurrentState = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                StageId = reader.GetInt32(5),
                StageName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                LastAutomationReason = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                LastAutomationOrigin = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                ActiveTimerCode = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                ActiveTimerDueAtUtc = ReadNullableUtcDateTime(reader, 10)
            };
            dispatchSnapshotJson = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
        }

        var interactionType = AdminKanbanJourneyDispatchInteractionTypes.Normalize(request.InteractionType);
        var occurredAtUtc = NormalizeJourneyUtc(request.OccurredAtUtc) ?? DateTime.UtcNow;
        var actionSource = string.IsNullOrWhiteSpace(request.SourceChannel) ? match.SourceChannel : request.SourceChannel.Trim();
        var dispatch = DeserializeJourneyDispatchSnapshot(dispatchSnapshotJson);
        var target = dispatch.Targets.FirstOrDefault(item =>
            string.Equals(item.TargetKey, request.TargetKey, StringComparison.Ordinal) &&
            item.ProviderId == request.ProviderId);

        if (target is null)
        {
            transaction.Rollback();
            return new AdminKanbanJourneyDispatchTargetInteractionResult
            {
                Succeeded = false,
                TargetUnavailable = true,
                LeadId = request.LeadId,
                JourneyId = match.JourneyId,
                CurrentState = match.CurrentState,
                Message = "O alvo da oportunidade nao esta mais disponivel."
            };
        }

        if (interactionType == AdminKanbanJourneyDispatchInteractionTypes.Declined)
        {
            if (dispatch.ReservedProviderId.HasValue && dispatch.ReservedProviderId.Value != request.ProviderId)
            {
                transaction.Commit();
                return new AdminKanbanJourneyDispatchTargetInteractionResult
                {
                    Succeeded = false,
                    AlreadyReserved = true,
                    LeadId = match.LeadId,
                    JourneyId = match.JourneyId,
                    CurrentState = AdminKanbanJourneyStates.ProviderConnected,
                    TargetStatus = target.Status,
                    Message = "A oportunidade ja foi reservada por outro prestador.",
                    ReservedProviderId = dispatch.ReservedProviderId,
                    ReservedProviderName = dispatch.ReservedProviderName
                };
            }

            if (!string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Sent, StringComparison.OrdinalIgnoreCase))
            {
                transaction.Commit();
                return new AdminKanbanJourneyDispatchTargetInteractionResult
                {
                    Succeeded = false,
                    AlreadyResponded = true,
                    LeadId = match.LeadId,
                    JourneyId = match.JourneyId,
                    CurrentState = match.CurrentState,
                    TargetStatus = target.Status,
                    Message = "A oportunidade ja recebeu uma resposta definitiva para este prestador.",
                    ReservedProviderId = dispatch.ReservedProviderId,
                    ReservedProviderName = dispatch.ReservedProviderName
                };
            }
        }

        var updatedDispatch = interactionType switch
        {
            AdminKanbanJourneyDispatchInteractionTypes.Opened => ApplyOpenTracking(dispatch, target, occurredAtUtc, actionSource),
            AdminKanbanJourneyDispatchInteractionTypes.Clicked => ApplyClickTracking(dispatch, target, occurredAtUtc, actionSource),
            AdminKanbanJourneyDispatchInteractionTypes.Declined => ApplyDeclineTracking(dispatch, target, occurredAtUtc, actionSource),
            _ => dispatch
        };

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_executions
SET DispatchStatus = @dispatchStatus,
    DispatchSummary = @dispatchSummary,
    DispatchCurrentWaveNumber = @dispatchCurrentWaveNumber,
    DispatchMaxWaveNumber = @dispatchMaxWaveNumber,
    DispatchSentTargets = @dispatchSentTargets,
    DispatchAcceptedTargets = @dispatchAcceptedTargets,
    DispatchDeclinedTargets = @dispatchDeclinedTargets,
    DispatchExpiredTargets = @dispatchExpiredTargets,
    DispatchPendingTargets = @dispatchPendingTargets,
    DispatchWaitingAcceptanceUntilUtc = @dispatchWaitingAcceptanceUntilUtc,
    DispatchSnapshotJson = @dispatchSnapshotJson,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @journeyId;
""";
            updateCommand.Parameters.AddRange(
            [
                new SqlParameter("@journeyId", SqlDbType.Int) { Value = match.JourneyId },
                new SqlParameter("@dispatchStatus", SqlDbType.NVarChar, 40) { Value = updatedDispatch.Status },
                new SqlParameter("@dispatchSummary", SqlDbType.NVarChar, 500) { Value = updatedDispatch.Summary },
                new SqlParameter("@dispatchCurrentWaveNumber", SqlDbType.Int) { Value = updatedDispatch.CurrentWaveNumber },
                new SqlParameter("@dispatchMaxWaveNumber", SqlDbType.Int) { Value = updatedDispatch.MaxWaveNumber },
                new SqlParameter("@dispatchSentTargets", SqlDbType.Int) { Value = updatedDispatch.SentTargetsCount },
                new SqlParameter("@dispatchAcceptedTargets", SqlDbType.Int) { Value = updatedDispatch.AcceptedTargetsCount },
                new SqlParameter("@dispatchDeclinedTargets", SqlDbType.Int) { Value = updatedDispatch.DeclinedTargetsCount },
                new SqlParameter("@dispatchExpiredTargets", SqlDbType.Int) { Value = updatedDispatch.ExpiredTargetsCount },
                new SqlParameter("@dispatchPendingTargets", SqlDbType.Int) { Value = updatedDispatch.PendingTargetsCount },
                new SqlParameter("@dispatchWaitingAcceptanceUntilUtc", SqlDbType.DateTime2) { Value = updatedDispatch.WaitingAcceptanceUntilUtc.HasValue ? updatedDispatch.WaitingAcceptanceUntilUtc.Value : DBNull.Value },
                new SqlParameter("@dispatchSnapshotJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneyDispatchSnapshot(updatedDispatch)) }
            ]);
            updateCommand.ExecuteNonQuery();
        }

        if (interactionType == AdminKanbanJourneyDispatchInteractionTypes.Declined)
        {
            InsertJourneyEventRecord(
                connection,
                transaction,
                match.JourneyId,
                match.LeadId,
                "jornada_disparo_recusado_link",
                match.CurrentState,
                match.CurrentState,
                actionSource,
                $"Prestador recusou a oportunidade via link assinado. {target.ProviderName}",
                $$"""
{"providerId":"{{request.ProviderId}}","targetKey":"{{request.TargetKey}}","source":"{{actionSource}}"}
""");

            InsertHistory(
                connection,
                transaction,
                match.LeadId,
                "jornada_disparo_recusado_link",
                null,
                null,
                $"Prestador {target.ProviderName} recusou a oportunidade via link assinado.");
        }

        transaction.Commit();

        var updatedTarget = updatedDispatch.Targets.FirstOrDefault(item =>
            string.Equals(item.TargetKey, request.TargetKey, StringComparison.Ordinal) &&
            item.ProviderId == request.ProviderId) ?? target;

        return new AdminKanbanJourneyDispatchTargetInteractionResult
        {
            Succeeded = true,
            LeadId = match.LeadId,
            JourneyId = match.JourneyId,
            CurrentState = match.CurrentState,
            TargetStatus = updatedTarget.Status,
            Message = interactionType switch
            {
                AdminKanbanJourneyDispatchInteractionTypes.Opened => "Abertura do e-mail registrada.",
                AdminKanbanJourneyDispatchInteractionTypes.Clicked => "Clique do prestador registrado.",
                AdminKanbanJourneyDispatchInteractionTypes.Declined => "Recusa do prestador registrada.",
                _ => "Interacao registrada."
            },
            ReservedProviderId = updatedDispatch.ReservedProviderId,
            ReservedProviderName = updatedDispatch.ReservedProviderName
        };
    }

    private static AdminKanbanJourneyDispatchRecord ApplyOpenTracking(
        AdminKanbanJourneyDispatchRecord dispatch,
        AdminKanbanJourneyDispatchTargetRecord target,
        DateTime occurredAtUtc,
        string actionSource)
    {
        var updatedTarget = target with
        {
            DeliveryStatus = ResolveDeliveryStatus(target, AdminKanbanJourneyDispatchInteractionTypes.Opened),
            OpenedAtUtc = occurredAtUtc,
            OpenCount = target.OpenCount + 1,
            LastInteractionSource = TrimTo(actionSource, 40),
            LastInteractionAtUtc = occurredAtUtc
        };

        return RefreshDispatchSnapshot(dispatch with
        {
            Targets = dispatch.Targets
                .Select(item => item.ProviderId == target.ProviderId && string.Equals(item.TargetKey, target.TargetKey, StringComparison.Ordinal)
                    ? updatedTarget
                    : item)
                .ToList()
        });
    }

    private static AdminKanbanJourneyDispatchRecord ApplyClickTracking(
        AdminKanbanJourneyDispatchRecord dispatch,
        AdminKanbanJourneyDispatchTargetRecord target,
        DateTime occurredAtUtc,
        string actionSource)
    {
        var updatedTarget = target with
        {
            DeliveryStatus = ResolveDeliveryStatus(target, AdminKanbanJourneyDispatchInteractionTypes.Clicked),
            ClickedAtUtc = occurredAtUtc,
            ClickCount = target.ClickCount + 1,
            LastInteractionSource = TrimTo(actionSource, 40),
            LastInteractionAtUtc = occurredAtUtc
        };

        return RefreshDispatchSnapshot(dispatch with
        {
            Targets = dispatch.Targets
                .Select(item => item.ProviderId == target.ProviderId && string.Equals(item.TargetKey, target.TargetKey, StringComparison.Ordinal)
                    ? updatedTarget
                    : item)
                .ToList()
        });
    }

    private static AdminKanbanJourneyDispatchRecord ApplyDeclineTracking(
        AdminKanbanJourneyDispatchRecord dispatch,
        AdminKanbanJourneyDispatchTargetRecord target,
        DateTime occurredAtUtc,
        string actionSource)
    {
        var updatedTargets = dispatch.Targets
            .Select(item => item.ProviderId == target.ProviderId && string.Equals(item.TargetKey, target.TargetKey, StringComparison.Ordinal)
                ? item with
                {
                    Status = AdminKanbanJourneyDispatchTargetStatuses.Declined,
                    DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Declined,
                    RespondedAtUtc = occurredAtUtc,
                    LastInteractionSource = TrimTo(actionSource, 40),
                    LastInteractionAtUtc = occurredAtUtc,
                    LastError = string.Empty,
                    Note = "Prestador recusou a oportunidade via link assinado."
                }
                : item)
            .ToList();

        var pendingTargetsCount = updatedTargets.Count(item =>
            string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Sent, StringComparison.OrdinalIgnoreCase));
        var summary = pendingTargetsCount > 0
            ? $"Prestador {target.ProviderName} recusou a oportunidade. Ainda existem alvos pendentes na onda atual."
            : $"Prestador {target.ProviderName} recusou a oportunidade. A proxima onda pode ser liberada imediatamente.";

        return RefreshDispatchSnapshot(
            dispatch with
            {
                Summary = summary,
                Targets = updatedTargets,
                WaitingAcceptanceUntilUtc = pendingTargetsCount > 0 ? dispatch.WaitingAcceptanceUntilUtc : occurredAtUtc
            },
            summaryOverride: summary,
            waitingAcceptanceUntilUtc: pendingTargetsCount > 0 ? dispatch.WaitingAcceptanceUntilUtc : occurredAtUtc);
    }

    private static AdminKanbanJourneyDispatchRecord RefreshDispatchSnapshot(
        AdminKanbanJourneyDispatchRecord dispatch,
        string? summaryOverride = null,
        DateTime? waitingAcceptanceUntilUtc = null)
    {
        var normalizedTargets = dispatch.Targets
            .OrderBy(item => item.WaveNumber)
            .ThenBy(item => item.RankPosition <= 0 ? int.MaxValue : item.RankPosition)
            .Select(item => item with
            {
                Status = AdminKanbanJourneyDispatchTargetStatuses.Normalize(item.Status),
                DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Normalize(item.DeliveryStatus)
            })
            .ToList();
        var normalizedWaves = dispatch.Waves
            .OrderBy(item => item.WaveNumber)
            .Select(item => item with
            {
                Status = AdminKanbanJourneyDispatchWaveStatuses.Normalize(item.Status)
            })
            .ToList();

        return new AdminKanbanJourneyDispatchRecord
        {
            Status = string.IsNullOrWhiteSpace(dispatch.Status)
                ? AdminKanbanJourneyDispatchStatuses.WaitingAcceptance
                : AdminKanbanJourneyDispatchStatuses.Normalize(dispatch.Status),
            Summary = string.IsNullOrWhiteSpace(summaryOverride) ? dispatch.Summary : summaryOverride,
            Strategy = dispatch.Strategy,
            EligibleProvidersCount = dispatch.EligibleProvidersCount,
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
            LastWaveQueuedAtUtc = dispatch.LastWaveQueuedAtUtc,
            WaitingAcceptanceUntilUtc = waitingAcceptanceUntilUtc ?? dispatch.WaitingAcceptanceUntilUtc,
            ReservedProviderId = dispatch.ReservedProviderId,
            ReservedProviderName = dispatch.ReservedProviderName,
            ReservedProviderEmail = dispatch.ReservedProviderEmail,
            ReservedProviderPhone = dispatch.ReservedProviderPhone,
            ReservedAtUtc = dispatch.ReservedAtUtc,
            Waves = normalizedWaves,
            Targets = normalizedTargets
        };
    }

    private static string ResolveDeliveryStatus(AdminKanbanJourneyDispatchTargetRecord target, string interactionType)
    {
        if (string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Accepted, StringComparison.OrdinalIgnoreCase))
        {
            return AdminKanbanJourneyDispatchDeliveryStatuses.Accepted;
        }

        if (string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Declined, StringComparison.OrdinalIgnoreCase))
        {
            return AdminKanbanJourneyDispatchDeliveryStatuses.Declined;
        }

        if (string.Equals(target.DeliveryStatus, AdminKanbanJourneyDispatchDeliveryStatuses.Failed, StringComparison.OrdinalIgnoreCase))
        {
            return AdminKanbanJourneyDispatchDeliveryStatuses.Failed;
        }

        return interactionType == AdminKanbanJourneyDispatchInteractionTypes.Opened
            ? AdminKanbanJourneyDispatchDeliveryStatuses.Opened
            : AdminKanbanJourneyDispatchDeliveryStatuses.Clicked;
    }
}
