using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed partial class SqlAdminKanbanService
{
    public AdminKanbanJourneyDispatchUpdateResult? UpdateJourneyDispatch(int leadId, AdminKanbanJourneyDispatchUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var match = TryGetJourneyMatchByLeadId(connection, transaction, leadId);
        if (match is null)
        {
            return null;
        }

        var normalizedCurrentState = string.IsNullOrWhiteSpace(request.CurrentState)
            ? AdminKanbanJourneyStates.Normalize(match.CurrentState)
            : AdminKanbanJourneyStates.Normalize(request.CurrentState);
        var normalizedDispatchStatus = NormalizeJourneyDispatchStatus(request.Status);
        var normalizedSourceChannel = string.IsNullOrWhiteSpace(request.SourceChannel)
            ? string.IsNullOrWhiteSpace(match.SourceChannel)
                ? AdminKanbanJourneySourceChannels.Telegram
                : AdminKanbanJourneySourceChannels.Normalize(match.SourceChannel)
            : AdminKanbanJourneySourceChannels.Normalize(request.SourceChannel);

        var dispatchRecord = new AdminKanbanJourneyDispatchRecord
        {
            Status = normalizedDispatchStatus,
            Summary = TrimToOrNull(request.Summary, 500) ?? string.Empty,
            Strategy = TrimToOrNull(request.Strategy, 80) ?? string.Empty,
            EligibleProvidersCount = Math.Max(0, request.EligibleProvidersCount),
            TargetsCreatedCount = Math.Max(0, request.TargetsCreatedCount),
            CurrentWaveNumber = Math.Max(0, request.CurrentWaveNumber),
            MaxWaveNumber = Math.Max(0, request.MaxWaveNumber),
            SentTargetsCount = Math.Max(0, request.SentTargetsCount),
            AcceptedTargetsCount = Math.Max(0, request.AcceptedTargetsCount),
            DeclinedTargetsCount = Math.Max(0, request.DeclinedTargetsCount),
            ExpiredTargetsCount = Math.Max(0, request.ExpiredTargetsCount),
            PendingTargetsCount = Math.Max(0, request.PendingTargetsCount),
            LastWaveQueuedAtUtc = NormalizeJourneyUtc(request.LastWaveQueuedAtUtc),
            WaitingAcceptanceUntilUtc = NormalizeJourneyUtc(request.WaitingAcceptanceUntilUtc),
            ReservedProviderId = request.ReservedProviderId,
            ReservedProviderName = TrimToOrNull(request.ReservedProviderName, 160) ?? string.Empty,
            ReservedProviderEmail = TrimToOrNull(request.ReservedProviderEmail, 180) ?? string.Empty,
            ReservedProviderPhone = TrimToOrNull(request.ReservedProviderPhone, 30) ?? string.Empty,
            ReservedAtUtc = NormalizeJourneyUtc(request.ReservedAtUtc),
            Waves = request.Waves
                .OrderBy(item => item.WaveNumber)
                .Select(item => new AdminKanbanJourneyDispatchWaveRecord
                {
                    WaveNumber = Math.Max(0, item.WaveNumber),
                    Status = NormalizeJourneyDispatchWaveStatus(item.Status),
                    EligibleSnapshotCount = Math.Max(0, item.EligibleSnapshotCount),
                    TargetCount = Math.Max(0, item.TargetCount),
                    CreatedAtUtc = NormalizeJourneyUtc(item.CreatedAtUtc) ?? item.CreatedAtUtc,
                    ActivatedAtUtc = NormalizeJourneyUtc(item.ActivatedAtUtc),
                    ExpiresAtUtc = NormalizeJourneyUtc(item.ExpiresAtUtc),
                    CompletedAtUtc = NormalizeJourneyUtc(item.CompletedAtUtc),
                    Summary = TrimTo(item.Summary, 260)
                })
                .ToList(),
            Targets = request.Targets
                .OrderBy(item => item.WaveNumber)
                .ThenBy(item => item.RankPosition <= 0 ? int.MaxValue : item.RankPosition)
                .Select(item => new AdminKanbanJourneyDispatchTargetRecord
                {
                    TargetKey = TrimTo(item.TargetKey, 180),
                    ProviderId = item.ProviderId,
                    ProviderName = TrimTo(item.ProviderName, 160),
                    ProviderEmail = TrimTo(item.ProviderEmail, 180),
                    ProviderPhone = TrimTo(item.ProviderPhone, 30),
                    RankPosition = Math.Max(0, item.RankPosition),
                    WaveNumber = Math.Max(0, item.WaveNumber),
                    Status = NormalizeJourneyDispatchTargetStatus(item.Status),
                    CreatedAtUtc = NormalizeJourneyUtc(item.CreatedAtUtc) ?? item.CreatedAtUtc,
                    SentAtUtc = NormalizeJourneyUtc(item.SentAtUtc),
                    RespondedAtUtc = NormalizeJourneyUtc(item.RespondedAtUtc),
                    ExpiresAtUtc = NormalizeJourneyUtc(item.ExpiresAtUtc),
                    Note = TrimTo(item.Note, 260)
                })
                .ToList()
        };

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_executions
SET CurrentState = @currentState,
    DispatchStatus = @dispatchStatus,
    DispatchSummary = @dispatchSummary,
    DispatchStrategy = @dispatchStrategy,
    DispatchEligibleProviders = @dispatchEligibleProviders,
    DispatchTargetsCreated = @dispatchTargetsCreated,
    DispatchCurrentWaveNumber = @dispatchCurrentWaveNumber,
    DispatchMaxWaveNumber = @dispatchMaxWaveNumber,
    DispatchSentTargets = @dispatchSentTargets,
    DispatchAcceptedTargets = @dispatchAcceptedTargets,
    DispatchDeclinedTargets = @dispatchDeclinedTargets,
    DispatchExpiredTargets = @dispatchExpiredTargets,
    DispatchPendingTargets = @dispatchPendingTargets,
    DispatchLastWaveQueuedAtUtc = @dispatchLastWaveQueuedAtUtc,
    DispatchWaitingAcceptanceUntilUtc = @dispatchWaitingAcceptanceUntilUtc,
    DispatchReservedProviderId = @dispatchReservedProviderId,
    DispatchReservedProviderName = @dispatchReservedProviderName,
    DispatchReservedProviderEmail = @dispatchReservedProviderEmail,
    DispatchReservedProviderPhone = @dispatchReservedProviderPhone,
    DispatchReservedAtUtc = @dispatchReservedAtUtc,
    DispatchSnapshotJson = @dispatchSnapshotJson,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @journeyId;
""";
            command.Parameters.AddRange(
            [
                new SqlParameter("@journeyId", SqlDbType.Int) { Value = match.JourneyId },
                new SqlParameter("@currentState", SqlDbType.NVarChar, 40) { Value = normalizedCurrentState },
                new SqlParameter("@dispatchStatus", SqlDbType.NVarChar, 40) { Value = ToDbValue(normalizedDispatchStatus) },
                new SqlParameter("@dispatchSummary", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(dispatchRecord.Summary, 500)) },
                new SqlParameter("@dispatchStrategy", SqlDbType.NVarChar, 80) { Value = ToDbValue(TrimToOrNull(dispatchRecord.Strategy, 80)) },
                new SqlParameter("@dispatchEligibleProviders", SqlDbType.Int) { Value = dispatchRecord.EligibleProvidersCount },
                new SqlParameter("@dispatchTargetsCreated", SqlDbType.Int) { Value = dispatchRecord.TargetsCreatedCount },
                new SqlParameter("@dispatchCurrentWaveNumber", SqlDbType.Int) { Value = dispatchRecord.CurrentWaveNumber },
                new SqlParameter("@dispatchMaxWaveNumber", SqlDbType.Int) { Value = dispatchRecord.MaxWaveNumber },
                new SqlParameter("@dispatchSentTargets", SqlDbType.Int) { Value = dispatchRecord.SentTargetsCount },
                new SqlParameter("@dispatchAcceptedTargets", SqlDbType.Int) { Value = dispatchRecord.AcceptedTargetsCount },
                new SqlParameter("@dispatchDeclinedTargets", SqlDbType.Int) { Value = dispatchRecord.DeclinedTargetsCount },
                new SqlParameter("@dispatchExpiredTargets", SqlDbType.Int) { Value = dispatchRecord.ExpiredTargetsCount },
                new SqlParameter("@dispatchPendingTargets", SqlDbType.Int) { Value = dispatchRecord.PendingTargetsCount },
                new SqlParameter("@dispatchLastWaveQueuedAtUtc", SqlDbType.DateTime2) { Value = dispatchRecord.LastWaveQueuedAtUtc.HasValue ? dispatchRecord.LastWaveQueuedAtUtc.Value : DBNull.Value },
                new SqlParameter("@dispatchWaitingAcceptanceUntilUtc", SqlDbType.DateTime2) { Value = dispatchRecord.WaitingAcceptanceUntilUtc.HasValue ? dispatchRecord.WaitingAcceptanceUntilUtc.Value : DBNull.Value },
                new SqlParameter("@dispatchReservedProviderId", SqlDbType.UniqueIdentifier) { Value = dispatchRecord.ReservedProviderId.HasValue ? dispatchRecord.ReservedProviderId.Value : DBNull.Value },
                new SqlParameter("@dispatchReservedProviderName", SqlDbType.NVarChar, 160) { Value = ToDbValue(TrimToOrNull(dispatchRecord.ReservedProviderName, 160)) },
                new SqlParameter("@dispatchReservedProviderEmail", SqlDbType.NVarChar, 180) { Value = ToDbValue(TrimToOrNull(dispatchRecord.ReservedProviderEmail, 180)) },
                new SqlParameter("@dispatchReservedProviderPhone", SqlDbType.NVarChar, 30) { Value = ToDbValue(TrimToOrNull(dispatchRecord.ReservedProviderPhone, 30)) },
                new SqlParameter("@dispatchReservedAtUtc", SqlDbType.DateTime2) { Value = dispatchRecord.ReservedAtUtc.HasValue ? dispatchRecord.ReservedAtUtc.Value : DBNull.Value },
                new SqlParameter("@dispatchSnapshotJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneyDispatchSnapshot(dispatchRecord)) }
            ]);
            command.ExecuteNonQuery();
        }

        if (!string.IsNullOrWhiteSpace(request.HistoryEventType) && !string.IsNullOrWhiteSpace(request.HistoryDescription))
        {
            InsertJourneyEventRecord(
                connection,
                transaction,
                match.JourneyId,
                leadId,
                request.HistoryEventType,
                match.CurrentState,
                normalizedCurrentState,
                normalizedSourceChannel,
                request.HistoryDescription,
                request.MetadataJson);

            InsertHistory(connection, transaction, leadId, request.HistoryEventType, null, null, request.HistoryDescription);
        }

        transaction.Commit();

        return new AdminKanbanJourneyDispatchUpdateResult
        {
            LeadId = leadId,
            JourneyId = match.JourneyId,
            CurrentState = normalizedCurrentState,
            Dispatch = dispatchRecord
        };
    }

    public AdminKanbanJourneyDispatchQueueItemRecord EnqueueJourneyDispatchQueueItem(AdminKanbanJourneyDispatchQueueEnqueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        var normalizedTargetKey = TrimTo(request.TargetKey, 180);
        var payloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson;
        var normalizedNextAttemptAt = NormalizeJourneyUtc(request.NextAttemptAt) ?? DateTime.UtcNow;
        var normalizedLastError = TrimTo(request.LastError, 1000);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        AdminKanbanJourneyDispatchQueueItemRecord? existing = null;
        using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = $"""
SELECT TOP (1)
    Id,
    LeadId,
    JourneyId,
    WaveNumber,
    ProviderId,
    TargetKey,
    PayloadJson,
    Status,
    AttemptCount,
    MaxAttempts,
    NextAttemptAt,
    LastAttemptAt,
    LastError,
    WorkerInstance,
    CreatedAt,
    UpdatedAt,
    ProcessedAt,
    DeadLetterAt
FROM dbo.{TablePrefix}journey_dispatch_queue
WHERE TargetKey = @targetKey
ORDER BY Id DESC;
""";
            existingCommand.Parameters.Add(new SqlParameter("@targetKey", SqlDbType.NVarChar, 180) { Value = normalizedTargetKey });

            using var reader = existingCommand.ExecuteReader();
            if (reader.Read())
            {
                existing = ReadJourneyDispatchQueueItemRecord(reader, true);
            }
        }

        if (existing is not null)
        {
            transaction.Commit();
            return existing;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}journey_dispatch_queue
(LeadId, JourneyId, WaveNumber, ProviderId, TargetKey, PayloadJson, Status, AttemptCount, MaxAttempts, NextAttemptAt, LastAttemptAt, LastError, WorkerInstance, CreatedAt, UpdatedAt, ProcessedAt, DeadLetterAt)
OUTPUT
    inserted.Id,
    inserted.LeadId,
    inserted.JourneyId,
    inserted.WaveNumber,
    inserted.ProviderId,
    inserted.TargetKey,
    inserted.PayloadJson,
    inserted.Status,
    inserted.AttemptCount,
    inserted.MaxAttempts,
    inserted.NextAttemptAt,
    inserted.LastAttemptAt,
    inserted.LastError,
    inserted.WorkerInstance,
    inserted.CreatedAt,
    inserted.UpdatedAt,
    inserted.ProcessedAt,
    inserted.DeadLetterAt
VALUES
(@leadId, @journeyId, @waveNumber, @providerId, @targetKey, @payloadJson, @status, 0, @maxAttempts, @nextAttemptAt, NULL, @lastError, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, NULL);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@leadId", SqlDbType.Int) { Value = request.LeadId },
            new SqlParameter("@journeyId", SqlDbType.Int) { Value = request.JourneyId },
            new SqlParameter("@waveNumber", SqlDbType.Int) { Value = Math.Max(0, request.WaveNumber) },
            new SqlParameter("@providerId", SqlDbType.UniqueIdentifier) { Value = request.ProviderId },
            new SqlParameter("@targetKey", SqlDbType.NVarChar, 180) { Value = normalizedTargetKey },
            new SqlParameter("@payloadJson", SqlDbType.NVarChar, -1) { Value = payloadJson },
            new SqlParameter("@status", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Pending },
            new SqlParameter("@maxAttempts", SqlDbType.Int) { Value = Math.Max(1, request.MaxAttempts) },
            new SqlParameter("@nextAttemptAt", SqlDbType.DateTime2) { Value = normalizedNextAttemptAt },
            new SqlParameter("@lastError", SqlDbType.NVarChar, 1000) { Value = ToDbValue(TrimToOrNull(normalizedLastError, 1000)) }
        ]);

        using var insertedReader = command.ExecuteReader();
        insertedReader.Read();
        var queueItem = ReadJourneyDispatchQueueItemRecord(insertedReader, false);

        transaction.Commit();
        return queueItem;
    }

    public IReadOnlyList<AdminKanbanJourneyDispatchQueueItemRecord> AcquireDueJourneyDispatchQueueItems(int batchSize, DateTime attemptStartedAtUtc, string workerInstance)
    {
        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        var effectiveBatchSize = Math.Clamp(batchSize, 1, 200);
        var normalizedAttemptStartedAt = NormalizeJourneyUtc(attemptStartedAtUtc) ?? DateTime.UtcNow;
        var items = new List<AdminKanbanJourneyDispatchQueueItemRecord>(effectiveBatchSize);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
;WITH due AS
(
    SELECT TOP (@batchSize) *
    FROM dbo.{TablePrefix}journey_dispatch_queue WITH (READPAST, UPDLOCK, ROWLOCK)
    WHERE Status IN (@pendingStatus, @retryingStatus)
      AND NextAttemptAt <= @attemptStartedAtUtc
    ORDER BY NextAttemptAt, Id
)
UPDATE due
SET Status = @processingStatus,
    AttemptCount = AttemptCount + 1,
    LastAttemptAt = @attemptStartedAtUtc,
    WorkerInstance = @workerInstance,
    UpdatedAt = SYSUTCDATETIME()
OUTPUT
    inserted.Id,
    inserted.LeadId,
    inserted.JourneyId,
    inserted.WaveNumber,
    inserted.ProviderId,
    inserted.TargetKey,
    inserted.PayloadJson,
    inserted.Status,
    inserted.AttemptCount,
    inserted.MaxAttempts,
    inserted.NextAttemptAt,
    inserted.LastAttemptAt,
    inserted.LastError,
    inserted.WorkerInstance,
    inserted.CreatedAt,
    inserted.UpdatedAt,
    inserted.ProcessedAt,
    inserted.DeadLetterAt;
""";
        command.Parameters.Add(new SqlParameter("@batchSize", SqlDbType.Int) { Value = effectiveBatchSize });
        command.Parameters.Add(new SqlParameter("@attemptStartedAtUtc", SqlDbType.DateTime2) { Value = normalizedAttemptStartedAt });
        command.Parameters.Add(new SqlParameter("@pendingStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Pending });
        command.Parameters.Add(new SqlParameter("@retryingStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Retrying });
        command.Parameters.Add(new SqlParameter("@processingStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Processing });
        command.Parameters.Add(new SqlParameter("@workerInstance", SqlDbType.NVarChar, 120) { Value = TrimTo(workerInstance, 120) });

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadJourneyDispatchQueueItemRecord(reader, false));
        }

        return items;
    }

    public AdminKanbanJourneyDispatchQueueItemRecord? FinalizeJourneyDispatchQueueItem(AdminKanbanJourneyDispatchQueueFinalizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_dispatch_queue
SET Status = @status,
    NextAttemptAt = @nextAttemptAt,
    LastError = CASE WHEN @clearLastError = 1 THEN NULL ELSE @lastError END,
    WorkerInstance = @workerInstance,
    UpdatedAt = SYSUTCDATETIME(),
    ProcessedAt = CASE WHEN @status = @processedStatus THEN @finalizedAt ELSE ProcessedAt END,
    DeadLetterAt = CASE WHEN @status = @deadLetterStatus THEN @finalizedAt ELSE DeadLetterAt END
OUTPUT
    inserted.Id,
    inserted.LeadId,
    inserted.JourneyId,
    inserted.WaveNumber,
    inserted.ProviderId,
    inserted.TargetKey,
    inserted.PayloadJson,
    inserted.Status,
    inserted.AttemptCount,
    inserted.MaxAttempts,
    inserted.NextAttemptAt,
    inserted.LastAttemptAt,
    inserted.LastError,
    inserted.WorkerInstance,
    inserted.CreatedAt,
    inserted.UpdatedAt,
    inserted.ProcessedAt,
    inserted.DeadLetterAt
WHERE Id = @queueItemId;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@queueItemId", SqlDbType.Int) { Value = request.QueueItemId },
            new SqlParameter("@status", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Normalize(request.FinalStatus) },
            new SqlParameter("@nextAttemptAt", SqlDbType.DateTime2) { Value = request.NextAttemptAt.HasValue ? NormalizeJourneyUtc(request.NextAttemptAt)!.Value : DBNull.Value },
            new SqlParameter("@lastError", SqlDbType.NVarChar, 1000) { Value = ToDbValue(TrimToOrNull(request.LastError, 1000)) },
            new SqlParameter("@clearLastError", SqlDbType.Bit) { Value = request.ClearLastError },
            new SqlParameter("@workerInstance", SqlDbType.NVarChar, 120) { Value = ToDbValue(TrimTo(request.WorkerInstance, 120)) },
            new SqlParameter("@finalizedAt", SqlDbType.DateTime2) { Value = NormalizeJourneyUtc(request.FinalizedAt) ?? DateTime.UtcNow },
            new SqlParameter("@processedStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Processed },
            new SqlParameter("@deadLetterStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.DeadLetter }
        ]);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? ReadJourneyDispatchQueueItemRecord(reader, false)
            : null;
    }

    public AdminKanbanJourneyDispatchReservationResult? TryReserveJourneyDispatchTarget(AdminKanbanJourneyDispatchReservationRequest request)
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

        var dispatch = DeserializeJourneyDispatchSnapshot(dispatchSnapshotJson);
        if (dispatch.ReservedProviderId.HasValue)
        {
            transaction.Commit();
            return new AdminKanbanJourneyDispatchReservationResult
            {
                Succeeded = false,
                AlreadyReserved = true,
                LeadId = match.LeadId,
                JourneyId = match.JourneyId,
                CurrentState = AdminKanbanJourneyStates.ProviderConnected,
                ReservedProviderId = dispatch.ReservedProviderId,
                ReservedProviderName = dispatch.ReservedProviderName
            };
        }

        var target = dispatch.Targets.FirstOrDefault(item =>
            string.Equals(item.TargetKey, request.TargetKey, StringComparison.Ordinal) &&
            item.ProviderId == request.ProviderId);
        if (target is null)
        {
            transaction.Rollback();
            return new AdminKanbanJourneyDispatchReservationResult
            {
                Succeeded = false,
                AlreadyReserved = false,
                LeadId = match.LeadId,
                JourneyId = match.JourneyId,
                CurrentState = match.CurrentState
            };
        }

        var reservedAtUtc = NormalizeJourneyUtc(request.ReservedAtUtc) ?? DateTime.UtcNow;
        var updatedDispatch = dispatch with
        {
            Waves = dispatch.Waves
                .Select(item => item with
                {
                    Status = item.WaveNumber == target.WaveNumber
                        ? AdminKanbanJourneyDispatchWaveStatuses.Accepted
                        : item.CompletedAtUtc.HasValue
                            ? item.Status
                            : AdminKanbanJourneyDispatchWaveStatuses.Stopped,
                    CompletedAtUtc = item.CompletedAtUtc ?? reservedAtUtc,
                    Summary = item.WaveNumber == target.WaveNumber
                        ? $"Onda {item.WaveNumber} reservada pelo prestador {target.ProviderName}."
                        : item.Summary
                })
                .ToList(),
            Targets = dispatch.Targets
                .Select(item => item.ProviderId == request.ProviderId && string.Equals(item.TargetKey, request.TargetKey, StringComparison.Ordinal)
                    ? item with
                    {
                        Status = AdminKanbanJourneyDispatchTargetStatuses.Accepted,
                        RespondedAtUtc = reservedAtUtc,
                        Note = "Prestador aceitou a oportunidade."
                    }
                    : string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(item.Status, AdminKanbanJourneyDispatchTargetStatuses.Sent, StringComparison.OrdinalIgnoreCase)
                        ? item with
                        {
                            Status = AdminKanbanJourneyDispatchTargetStatuses.Dispensed,
                            RespondedAtUtc = reservedAtUtc,
                            Note = "Dispensado porque outro prestador reservou o caso."
                        }
                        : item)
                .ToList(),
            Status = AdminKanbanJourneyDispatchStatuses.Reserved,
            Summary = $"Caso reservado pelo prestador {target.ProviderName}.",
            ReservedProviderId = request.ProviderId,
            ReservedProviderName = target.ProviderName,
            ReservedProviderEmail = target.ProviderEmail,
            ReservedProviderPhone = target.ProviderPhone,
            ReservedAtUtc = reservedAtUtc,
            WaitingAcceptanceUntilUtc = null
        };

        var targetStageId = GetStageIdByName(connection, transaction, match.BoardType, AdminKanbanJourneyClientStageNames.ProviderConnected);

        using (var updateLeadCommand = connection.CreateCommand())
        {
            updateLeadCommand.Transaction = transaction;
            updateLeadCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET StageId = @stageId,
    SortOrder = @sortOrder,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @leadId
  AND IsActive = 1;
""";
            updateLeadCommand.Parameters.Add(new SqlParameter("@stageId", SqlDbType.Int) { Value = targetStageId });
            updateLeadCommand.Parameters.Add(new SqlParameter("@sortOrder", SqlDbType.Int) { Value = GetNextLeadSortOrder(connection, transaction, match.BoardType, targetStageId) });
            updateLeadCommand.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = match.LeadId });
            updateLeadCommand.ExecuteNonQuery();
        }

        using (var updateJourneyCommand = connection.CreateCommand())
        {
            updateJourneyCommand.Transaction = transaction;
            updateJourneyCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_executions
SET CurrentState = @currentState,
    DispatchStatus = @dispatchStatus,
    DispatchSummary = @dispatchSummary,
    DispatchCurrentWaveNumber = @dispatchCurrentWaveNumber,
    DispatchMaxWaveNumber = @dispatchMaxWaveNumber,
    DispatchSentTargets = @dispatchSentTargets,
    DispatchAcceptedTargets = @dispatchAcceptedTargets,
    DispatchDeclinedTargets = @dispatchDeclinedTargets,
    DispatchExpiredTargets = @dispatchExpiredTargets,
    DispatchPendingTargets = @dispatchPendingTargets,
    DispatchWaitingAcceptanceUntilUtc = NULL,
    DispatchReservedProviderId = @dispatchReservedProviderId,
    DispatchReservedProviderName = @dispatchReservedProviderName,
    DispatchReservedProviderEmail = @dispatchReservedProviderEmail,
    DispatchReservedProviderPhone = @dispatchReservedProviderPhone,
    DispatchReservedAtUtc = @dispatchReservedAtUtc,
    DispatchSnapshotJson = @dispatchSnapshotJson,
    LastStageAutomationReason = @reason,
    LastStageAutomationOrigin = @origin,
    LastStageAutomationAtUtc = @reservedAtUtc,
    ActiveTimerCode = NULL,
    ActiveTimerDueAtUtc = NULL,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @journeyId;
""";
            updateJourneyCommand.Parameters.AddRange(
            [
                new SqlParameter("@journeyId", SqlDbType.Int) { Value = match.JourneyId },
                new SqlParameter("@currentState", SqlDbType.NVarChar, 40) { Value = AdminKanbanJourneyStates.ProviderConnected },
                new SqlParameter("@dispatchStatus", SqlDbType.NVarChar, 40) { Value = AdminKanbanJourneyDispatchStatuses.Reserved },
                new SqlParameter("@dispatchSummary", SqlDbType.NVarChar, 500) { Value = updatedDispatch.Summary },
                new SqlParameter("@dispatchCurrentWaveNumber", SqlDbType.Int) { Value = updatedDispatch.CurrentWaveNumber },
                new SqlParameter("@dispatchMaxWaveNumber", SqlDbType.Int) { Value = updatedDispatch.MaxWaveNumber },
                new SqlParameter("@dispatchSentTargets", SqlDbType.Int) { Value = updatedDispatch.SentTargetsCount },
                new SqlParameter("@dispatchAcceptedTargets", SqlDbType.Int) { Value = updatedDispatch.AcceptedTargetsCount },
                new SqlParameter("@dispatchDeclinedTargets", SqlDbType.Int) { Value = updatedDispatch.DeclinedTargetsCount },
                new SqlParameter("@dispatchExpiredTargets", SqlDbType.Int) { Value = updatedDispatch.ExpiredTargetsCount },
                new SqlParameter("@dispatchPendingTargets", SqlDbType.Int) { Value = updatedDispatch.PendingTargetsCount },
                new SqlParameter("@dispatchReservedProviderId", SqlDbType.UniqueIdentifier) { Value = request.ProviderId },
                new SqlParameter("@dispatchReservedProviderName", SqlDbType.NVarChar, 160) { Value = updatedDispatch.ReservedProviderName },
                new SqlParameter("@dispatchReservedProviderEmail", SqlDbType.NVarChar, 180) { Value = ToDbValue(TrimToOrNull(updatedDispatch.ReservedProviderEmail, 180)) },
                new SqlParameter("@dispatchReservedProviderPhone", SqlDbType.NVarChar, 30) { Value = ToDbValue(TrimToOrNull(updatedDispatch.ReservedProviderPhone, 30)) },
                new SqlParameter("@dispatchReservedAtUtc", SqlDbType.DateTime2) { Value = reservedAtUtc },
                new SqlParameter("@dispatchSnapshotJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneyDispatchSnapshot(updatedDispatch)) },
                new SqlParameter("@reason", SqlDbType.NVarChar, 180) { Value = "Caso reservado por aceite valido de prestador." },
                new SqlParameter("@origin", SqlDbType.NVarChar, 40) { Value = AdminKanbanJourneyAutomationOrigins.DispatchEngine },
                new SqlParameter("@reservedAtUtc", SqlDbType.DateTime2) { Value = reservedAtUtc }
            ]);
            updateJourneyCommand.ExecuteNonQuery();
        }

        using (var queueCommand = connection.CreateCommand())
        {
            queueCommand.Transaction = transaction;
            queueCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_dispatch_queue
SET Status = @status,
    ProcessedAt = @processedAt,
    DeadLetterAt = NULL,
    LastError = @lastError,
    UpdatedAt = SYSUTCDATETIME()
WHERE LeadId = @leadId
  AND Status IN (@pendingStatus, @processingStatus, @retryingStatus);
""";
            queueCommand.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Processed });
            queueCommand.Parameters.Add(new SqlParameter("@processedAt", SqlDbType.DateTime2) { Value = reservedAtUtc });
            queueCommand.Parameters.Add(new SqlParameter("@lastError", SqlDbType.NVarChar, 1000) { Value = "Dispensado porque o caso foi reservado por um prestador." });
            queueCommand.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = match.LeadId });
            queueCommand.Parameters.Add(new SqlParameter("@pendingStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Pending });
            queueCommand.Parameters.Add(new SqlParameter("@processingStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Processing });
            queueCommand.Parameters.Add(new SqlParameter("@retryingStatus", SqlDbType.NVarChar, 30) { Value = AdminKanbanJourneyDispatchQueueStatuses.Retrying });
            queueCommand.ExecuteNonQuery();
        }

        var metadataJson = string.IsNullOrWhiteSpace(request.MetadataJson)
            ? JsonSerializer.Serialize(new { request.ProviderId, request.TargetKey })
            : request.MetadataJson;

        InsertJourneyEventRecord(
            connection,
            transaction,
            match.JourneyId,
            match.LeadId,
            "jornada_disparo_reservado",
            match.CurrentState,
            AdminKanbanJourneyStates.ProviderConnected,
            string.IsNullOrWhiteSpace(request.SourceChannel) ? match.SourceChannel : request.SourceChannel,
            $"Caso reservado pelo prestador {target.ProviderName}.",
            metadataJson);

        InsertHistory(
            connection,
            transaction,
            match.LeadId,
            "jornada_disparo_reservado",
            match.StageId,
            targetStageId,
            $"Caso reservado pelo prestador {target.ProviderName}.");

        transaction.Commit();

        return new AdminKanbanJourneyDispatchReservationResult
        {
            Succeeded = true,
            AlreadyReserved = false,
            LeadId = match.LeadId,
            JourneyId = match.JourneyId,
            CurrentState = AdminKanbanJourneyStates.ProviderConnected,
            ReservedProviderId = request.ProviderId,
            ReservedProviderName = target.ProviderName
        };
    }

    private void EnsureJourneyDispatchSchema(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
IF OBJECT_ID('dbo.{TablePrefix}journey_dispatch_queue', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}journey_dispatch_queue
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LeadId INT NOT NULL,
    JourneyId INT NOT NULL,
    WaveNumber INT NOT NULL,
    ProviderId UNIQUEIDENTIFIER NOT NULL,
    TargetKey NVARCHAR(180) NOT NULL,
    PayloadJson NVARCHAR(MAX) NOT NULL,
    Status NVARCHAR(30) NOT NULL,
    AttemptCount INT NOT NULL DEFAULT(0),
    MaxAttempts INT NOT NULL DEFAULT(3),
    NextAttemptAt DATETIME2 NOT NULL,
    LastAttemptAt DATETIME2 NULL,
    LastError NVARCHAR(1000) NULL,
    WorkerInstance NVARCHAR(120) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL,
    ProcessedAt DATETIME2 NULL,
    DeadLetterAt DATETIME2 NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_dispatch_queue') AND name = 'UX_{TablePrefix}journey_dispatch_queue_target')
CREATE UNIQUE INDEX UX_{TablePrefix}journey_dispatch_queue_target
    ON dbo.{TablePrefix}journey_dispatch_queue(TargetKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_dispatch_queue') AND name = 'IX_{TablePrefix}journey_dispatch_queue_due')
CREATE INDEX IX_{TablePrefix}journey_dispatch_queue_due
    ON dbo.{TablePrefix}journey_dispatch_queue(Status, NextAttemptAt, Id);

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchStatus') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchStatus NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchSummary') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchSummary NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchStrategy') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchStrategy NVARCHAR(80) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchEligibleProviders') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchEligibleProviders INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchTargetsCreated') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchTargetsCreated INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchCurrentWaveNumber') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchCurrentWaveNumber INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchMaxWaveNumber') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchMaxWaveNumber INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchSentTargets') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchSentTargets INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchAcceptedTargets') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchAcceptedTargets INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchDeclinedTargets') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchDeclinedTargets INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchExpiredTargets') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchExpiredTargets INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchPendingTargets') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchPendingTargets INT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchLastWaveQueuedAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchLastWaveQueuedAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchWaitingAcceptanceUntilUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchWaitingAcceptanceUntilUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchReservedProviderId') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchReservedProviderId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchReservedProviderName') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchReservedProviderName NVARCHAR(160) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchReservedProviderEmail') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchReservedProviderEmail NVARCHAR(180) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchReservedProviderPhone') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchReservedProviderPhone NVARCHAR(30) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchReservedAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchReservedAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'DispatchSnapshotJson') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD DispatchSnapshotJson NVARCHAR(MAX) NULL;
""";
        command.ExecuteNonQuery();
    }

    private static string NormalizeJourneyDispatchStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AdminKanbanJourneyDispatchStatuses.Normalize(value);

    private static string NormalizeJourneyDispatchWaveStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? AdminKanbanJourneyDispatchWaveStatuses.Queued
            : AdminKanbanJourneyDispatchWaveStatuses.Normalize(value);

    private static string NormalizeJourneyDispatchTargetStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? AdminKanbanJourneyDispatchTargetStatuses.Queued
            : AdminKanbanJourneyDispatchTargetStatuses.Normalize(value);

    private static string? SerializeJourneyDispatchSnapshot(AdminKanbanJourneyDispatchRecord? dispatch)
    {
        if (dispatch is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(dispatch);
    }

    private static AdminKanbanJourneyDispatchRecord DeserializeJourneyDispatchSnapshot(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new AdminKanbanJourneyDispatchRecord();
        }

        try
        {
            return JsonSerializer.Deserialize<AdminKanbanJourneyDispatchRecord>(payloadJson) ?? new AdminKanbanJourneyDispatchRecord();
        }
        catch
        {
            return new AdminKanbanJourneyDispatchRecord();
        }
    }

    private static AdminKanbanJourneyDispatchRecord ReadJourneyDispatchRecord(SqlDataReader reader, int startIndex)
    {
        var snapshotJson = reader.IsDBNull(startIndex + 19) ? string.Empty : reader.GetString(startIndex + 19);
        var snapshot = DeserializeJourneyDispatchSnapshot(snapshotJson);

        return snapshot with
        {
            Status = NormalizeJourneyDispatchStatus(reader.IsDBNull(startIndex) ? snapshot.Status : reader.GetString(startIndex)),
            Summary = reader.IsDBNull(startIndex + 1) ? snapshot.Summary : reader.GetString(startIndex + 1),
            Strategy = reader.IsDBNull(startIndex + 2) ? snapshot.Strategy : reader.GetString(startIndex + 2),
            EligibleProvidersCount = reader.IsDBNull(startIndex + 3) ? snapshot.EligibleProvidersCount : reader.GetInt32(startIndex + 3),
            TargetsCreatedCount = reader.IsDBNull(startIndex + 4) ? snapshot.TargetsCreatedCount : reader.GetInt32(startIndex + 4),
            CurrentWaveNumber = reader.IsDBNull(startIndex + 5) ? snapshot.CurrentWaveNumber : reader.GetInt32(startIndex + 5),
            MaxWaveNumber = reader.IsDBNull(startIndex + 6) ? snapshot.MaxWaveNumber : reader.GetInt32(startIndex + 6),
            SentTargetsCount = reader.IsDBNull(startIndex + 7) ? snapshot.SentTargetsCount : reader.GetInt32(startIndex + 7),
            AcceptedTargetsCount = reader.IsDBNull(startIndex + 8) ? snapshot.AcceptedTargetsCount : reader.GetInt32(startIndex + 8),
            DeclinedTargetsCount = reader.IsDBNull(startIndex + 9) ? snapshot.DeclinedTargetsCount : reader.GetInt32(startIndex + 9),
            ExpiredTargetsCount = reader.IsDBNull(startIndex + 10) ? snapshot.ExpiredTargetsCount : reader.GetInt32(startIndex + 10),
            PendingTargetsCount = reader.IsDBNull(startIndex + 11) ? snapshot.PendingTargetsCount : reader.GetInt32(startIndex + 11),
            LastWaveQueuedAtUtc = ReadNullableUtcDateTime(reader, startIndex + 12) ?? snapshot.LastWaveQueuedAtUtc,
            WaitingAcceptanceUntilUtc = ReadNullableUtcDateTime(reader, startIndex + 13) ?? snapshot.WaitingAcceptanceUntilUtc,
            ReservedProviderId = reader.IsDBNull(startIndex + 14) ? snapshot.ReservedProviderId : reader.GetGuid(startIndex + 14),
            ReservedProviderName = reader.IsDBNull(startIndex + 15) ? snapshot.ReservedProviderName : reader.GetString(startIndex + 15),
            ReservedProviderEmail = reader.IsDBNull(startIndex + 16) ? snapshot.ReservedProviderEmail : reader.GetString(startIndex + 16),
            ReservedProviderPhone = reader.IsDBNull(startIndex + 17) ? snapshot.ReservedProviderPhone : reader.GetString(startIndex + 17),
            ReservedAtUtc = ReadNullableUtcDateTime(reader, startIndex + 18) ?? snapshot.ReservedAtUtc
        };
    }

    private static AdminKanbanJourneyDispatchQueueItemRecord ReadJourneyDispatchQueueItemRecord(SqlDataReader reader, bool isDuplicate)
    {
        return new AdminKanbanJourneyDispatchQueueItemRecord
        {
            Id = reader.GetInt32(0),
            LeadId = reader.GetInt32(1),
            JourneyId = reader.GetInt32(2),
            WaveNumber = reader.GetInt32(3),
            ProviderId = reader.GetGuid(4),
            TargetKey = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            PayloadJson = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            Status = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            AttemptCount = reader.GetInt32(8),
            MaxAttempts = reader.GetInt32(9),
            NextAttemptAt = ReadAsUtcDateTime(reader, 10),
            LastAttemptAt = ReadNullableUtcDateTime(reader, 11),
            LastError = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
            WorkerInstance = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
            CreatedAt = ReadAsUtcDateTime(reader, 14),
            UpdatedAt = ReadNullableUtcDateTime(reader, 15),
            ProcessedAt = ReadNullableUtcDateTime(reader, 16),
            DeadLetterAt = ReadNullableUtcDateTime(reader, 17),
            IsDuplicate = isDuplicate
        };
    }
}
