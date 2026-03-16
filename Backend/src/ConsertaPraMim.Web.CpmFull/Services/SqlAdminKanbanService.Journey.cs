using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed partial class SqlAdminKanbanService
{
    private bool _journeyInitialized;

    public AdminKanbanJourneyUpsertResult UpsertJourneyIntake(AdminKanbanJourneyIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);
        var normalizedSourceChannel = AdminKanbanJourneySourceChannels.Normalize(request.SourceChannel);
        var requestedAtUtc = NormalizeJourneyUtc(request.RequestedAtUtc) ?? DateTime.UtcNow;
        var normalizedPhone = NormalizeJourneyPhone(request.Phone);
        var normalizedEmail = NormalizeJourneyEmail(request.Email);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var match = TryFindJourneyMatch(connection, transaction, normalizedBoardType, request, normalizedPhone, normalizedEmail, requestedAtUtc);
        var resolvedState = ResolveJourneyState(request, match?.CurrentState);
        var defaultStatusNote = BuildJourneyStatusNote(request, normalizedSourceChannel, resolvedState, match is not null);
        var isCrossChannelReentry = match is not null &&
            !string.IsNullOrWhiteSpace(match.SourceChannel) &&
            !string.Equals(match.SourceChannel, normalizedSourceChannel, StringComparison.OrdinalIgnoreCase);

        var createdLead = false;
        var createdJourney = false;
        int leadId;
        int stageId;
        int journeyId;
        Guid journeyPublicId;

        if (match is null)
        {
            stageId = ResolveStageId(connection, transaction, normalizedBoardType, 0);
            leadId = CreateJourneyLeadRecord(connection, transaction, normalizedBoardType, stageId, request, normalizedSourceChannel, defaultStatusNote, requestedAtUtc);
            createdLead = true;
            InsertHistory(
                connection,
                transaction,
                leadId,
                eventType: "jornada_criada",
                fromStageId: null,
                toStageId: stageId,
                description: BuildJourneyHistoryDescription(normalizedSourceChannel, resolvedState, createdLead: true, isCrossChannelReentry: false, serviceRequestLinked: request.ServiceRequestId.HasValue));
        }
        else
        {
            leadId = match.LeadId;
            stageId = match.StageId;
            UpdateJourneyLeadRecord(connection, transaction, leadId, request, normalizedSourceChannel, defaultStatusNote, requestedAtUtc, isCrossChannelReentry);
        }

        if (match is null || match.JourneyId <= 0 || match.JourneyPublicId == Guid.Empty)
        {
            (journeyId, journeyPublicId) = CreateJourneyExecutionRecord(connection, transaction, leadId, normalizedBoardType, request, normalizedSourceChannel, resolvedState, requestedAtUtc, normalizedPhone, normalizedEmail);
            createdJourney = true;
        }
        else
        {
            (journeyId, journeyPublicId) = UpdateJourneyExecutionRecord(connection, transaction, match, request, normalizedSourceChannel, resolvedState, requestedAtUtc, normalizedPhone, normalizedEmail);
        }

        if (string.Equals(normalizedSourceChannel, AdminKanbanJourneySourceChannels.Telegram, StringComparison.OrdinalIgnoreCase) &&
            request.ChatbotConversationId.HasValue &&
            request.ChatbotConversationId.Value != Guid.Empty &&
            request.TelegramChatId.HasValue &&
            request.TelegramChatId.Value > 0)
        {
            SaveTelegramLeadLink(connection, transaction, leadId, normalizedBoardType, new AdminKanbanTelegramLeadUpsertRequest
            {
                BoardType = normalizedBoardType,
                ChatbotConversationId = request.ChatbotConversationId.Value,
                ChannelConversationId = request.ChannelConversationId ?? string.Empty,
                TelegramChatId = request.TelegramChatId.Value,
                ClientId = request.ClientId ?? Guid.Empty,
                ClientName = string.IsNullOrWhiteSpace(request.Name) ? "Cliente Telegram" : request.Name,
                ClientPhone = request.Phone,
                ClientEmail = request.Email,
                ServiceRequestId = request.ServiceRequestId,
                ServiceCategory = request.ServiceCategory,
                PostalCode = request.PostalCode,
                City = request.City,
                StatusNote = defaultStatusNote,
                InternalNotes = request.InternalNotes,
                LastContactAt = NormalizeJourneyUtc(request.LastContactAtUtc) ?? requestedAtUtc
            });
        }

        var historyEventType = createdJourney
            ? "jornada_criada"
            : ResolveJourneyUpdateEventType(match, normalizedSourceChannel, request, resolvedState, isCrossChannelReentry);
        var historyDescription = BuildJourneyHistoryDescription(
            normalizedSourceChannel,
            resolvedState,
            createdLead,
            isCrossChannelReentry,
            request.ServiceRequestId.HasValue && !(match?.ServiceRequestId.HasValue ?? false));

        InsertJourneyEventRecord(
            connection,
            transaction,
            journeyId,
            leadId,
            historyEventType,
            match?.CurrentState,
            resolvedState,
            normalizedSourceChannel,
            historyDescription,
            BuildJourneyEventMetadataJson(request, normalizedPhone, normalizedEmail));

        if (!createdLead)
        {
            InsertHistory(connection, transaction, leadId, historyEventType, null, null, historyDescription);
        }

        transaction.Commit();

        return new AdminKanbanJourneyUpsertResult
        {
            LeadId = leadId,
            JourneyId = journeyId,
            JourneyPublicId = journeyPublicId,
            CreatedLead = createdLead,
            CreatedJourney = createdJourney,
            StageId = stageId,
            BoardType = normalizedBoardType,
            CurrentState = resolvedState
        };
    }

    public AdminKanbanLeadJourneyRecord? GetJourneyDetails(int leadId)
    {
        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1)
    Id,
    JourneyPublicId,
    LeadId,
    BoardType,
    JourneyKey,
    SourceChannel,
    SourceOrigin,
    CurrentState,
    LandingLeadId,
    ServiceRequestId,
    ClientId,
    VisitorId,
    SessionId,
    ChatbotConversationId,
    ChannelConversationId,
    TelegramChatId,
    PrimaryPhone,
    PrimaryEmail,
    CreatedAt,
    UpdatedAt,
    LastIntakeAt,
    QualificationStatus,
    QualificationSource,
    QualificationConfidenceScore,
    QualificationHasRequiredData,
    QualificationNeedsConfirmation,
    QualificationSummary,
    QualificationJson,
    QualificationQualifiedAt,
    SchedulingStatus,
    SchedulingSummary,
    GoogleCalendarEventId,
    GoogleCalendarEventLink,
    SuggestedSlotsJson,
    SuggestedAtUtc,
    SchedulingConfirmedAtUtc,
    SchedulingCancelledAtUtc,
    ScheduledStartAtUtc,
    ScheduledEndAtUtc,
    LastStageAutomationReason,
    LastStageAutomationOrigin,
    LastStageAutomationAtUtc,
    ActiveTimerCode,
    ActiveTimerDueAtUtc
FROM dbo.{TablePrefix}journey_executions
WHERE LeadId = @leadId
ORDER BY UpdatedAt DESC, Id DESC;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new AdminKanbanLeadJourneyRecord
        {
            JourneyId = reader.GetInt32(0),
            JourneyPublicId = reader.GetGuid(1),
            LeadId = reader.GetInt32(2),
            BoardType = reader.GetString(3),
            JourneyKey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            SourceChannel = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            SourceOrigin = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            CurrentState = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            LandingLeadId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
            ServiceRequestId = reader.IsDBNull(9) ? null : reader.GetGuid(9),
            ClientId = reader.IsDBNull(10) ? null : reader.GetGuid(10),
            VisitorId = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
            SessionId = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
            ChatbotConversationId = reader.IsDBNull(13) ? null : reader.GetGuid(13),
            ChannelConversationId = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
            TelegramChatId = ReadNullableInt64(reader, 15),
            PrimaryPhone = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
            PrimaryEmail = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
            CreatedAt = reader.GetDateTime(18),
            UpdatedAt = ReadNullableUtcDateTime(reader, 19),
            LastIntakeAt = ReadNullableUtcDateTime(reader, 20),
            Qualification = ReadJourneyQualificationRecord(reader, 21),
            Scheduling = ReadJourneySchedulingRecord(reader, 29),
            StageAutomation = new AdminKanbanJourneyStageAutomationRecord
            {
                LastReason = reader.IsDBNull(38) ? string.Empty : reader.GetString(38),
                LastOrigin = reader.IsDBNull(39) ? string.Empty : reader.GetString(39),
                LastTransitionAtUtc = ReadNullableUtcDateTime(reader, 40),
                ActiveTimerCode = reader.IsDBNull(41) ? string.Empty : reader.GetString(41),
                ActiveTimerDueAtUtc = ReadNullableUtcDateTime(reader, 42)
            }
        };
    }

    public AdminKanbanJourneySchedulingUpdateResult? UpdateJourneyScheduling(
        int leadId,
        AdminKanbanJourneySchedulingUpdateRequest request)
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
        var normalizedSchedulingStatus = NormalizeJourneySchedulingStatus(request.Status);
        var normalizedSourceChannel = string.IsNullOrWhiteSpace(request.SourceChannel)
            ? string.IsNullOrWhiteSpace(match.SourceChannel)
                ? AdminKanbanJourneySourceChannels.Telegram
                : AdminKanbanJourneySourceChannels.Normalize(match.SourceChannel)
            : AdminKanbanJourneySourceChannels.Normalize(request.SourceChannel);
        var schedulingRecord = new AdminKanbanJourneySchedulingRecord
        {
            Status = normalizedSchedulingStatus,
            Summary = TrimToOrNull(request.Summary, 500) ?? string.Empty,
            GoogleCalendarEventId = TrimToOrNull(request.GoogleCalendarEventId, 180) ?? string.Empty,
            GoogleCalendarEventLink = TrimToOrNull(request.GoogleCalendarEventLink, 500) ?? string.Empty,
            SuggestedAtUtc = NormalizeJourneyUtc(request.SuggestedAtUtc),
            ConfirmedAtUtc = NormalizeJourneyUtc(request.ConfirmedAtUtc),
            CancelledAtUtc = NormalizeJourneyUtc(request.CancelledAtUtc),
            ScheduledStartAtUtc = NormalizeJourneyUtc(request.ScheduledStartAtUtc),
            ScheduledEndAtUtc = NormalizeJourneyUtc(request.ScheduledEndAtUtc),
            SuggestedSlots = request.SuggestedSlots
                .OrderBy(item => item.OptionNumber)
                .Select(item => new AdminKanbanJourneySuggestedSlotRecord
                {
                    OptionNumber = item.OptionNumber,
                    StartsAtUtc = NormalizeJourneyUtc(item.StartsAtUtc) ?? item.StartsAtUtc,
                    EndsAtUtc = NormalizeJourneyUtc(item.EndsAtUtc) ?? item.EndsAtUtc,
                    Label = TrimTo(item.Label, 160)
                })
                .ToList()
        };

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_executions
SET CurrentState = @currentState,
    SchedulingStatus = @schedulingStatus,
    SchedulingSummary = @schedulingSummary,
    GoogleCalendarEventId = @googleCalendarEventId,
    GoogleCalendarEventLink = @googleCalendarEventLink,
    SuggestedSlotsJson = @suggestedSlotsJson,
    SuggestedAtUtc = @suggestedAtUtc,
    SchedulingConfirmedAtUtc = @confirmedAtUtc,
    SchedulingCancelledAtUtc = @cancelledAtUtc,
    ScheduledStartAtUtc = @scheduledStartAtUtc,
    ScheduledEndAtUtc = @scheduledEndAtUtc,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @journeyId;
""";
            command.Parameters.AddRange(
            [
                new SqlParameter("@journeyId", SqlDbType.Int) { Value = match.JourneyId },
                new SqlParameter("@currentState", SqlDbType.NVarChar, 40) { Value = normalizedCurrentState },
                new SqlParameter("@schedulingStatus", SqlDbType.NVarChar, 40) { Value = ToDbValue(normalizedSchedulingStatus) },
                new SqlParameter("@schedulingSummary", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(schedulingRecord.Summary, 500)) },
                new SqlParameter("@googleCalendarEventId", SqlDbType.NVarChar, 180) { Value = ToDbValue(TrimToOrNull(schedulingRecord.GoogleCalendarEventId, 180)) },
                new SqlParameter("@googleCalendarEventLink", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(schedulingRecord.GoogleCalendarEventLink, 500)) },
                new SqlParameter("@suggestedSlotsJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneySchedulingSlots(schedulingRecord.SuggestedSlots)) },
                new SqlParameter("@suggestedAtUtc", SqlDbType.DateTime2) { Value = schedulingRecord.SuggestedAtUtc.HasValue ? schedulingRecord.SuggestedAtUtc.Value : DBNull.Value },
                new SqlParameter("@confirmedAtUtc", SqlDbType.DateTime2) { Value = schedulingRecord.ConfirmedAtUtc.HasValue ? schedulingRecord.ConfirmedAtUtc.Value : DBNull.Value },
                new SqlParameter("@cancelledAtUtc", SqlDbType.DateTime2) { Value = schedulingRecord.CancelledAtUtc.HasValue ? schedulingRecord.CancelledAtUtc.Value : DBNull.Value },
                new SqlParameter("@scheduledStartAtUtc", SqlDbType.DateTime2) { Value = schedulingRecord.ScheduledStartAtUtc.HasValue ? schedulingRecord.ScheduledStartAtUtc.Value : DBNull.Value },
                new SqlParameter("@scheduledEndAtUtc", SqlDbType.DateTime2) { Value = schedulingRecord.ScheduledEndAtUtc.HasValue ? schedulingRecord.ScheduledEndAtUtc.Value : DBNull.Value }
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

        return new AdminKanbanJourneySchedulingUpdateResult
        {
            LeadId = leadId,
            JourneyId = match.JourneyId,
            CurrentState = normalizedCurrentState,
            Scheduling = schedulingRecord
        };
    }

    public IReadOnlyList<AdminKanbanJourneyStageAutomationCandidateRecord> ListJourneyStageAutomationCandidates(
        string boardType,
        DateTime nowUtc,
        int batchSize)
    {
        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(boardType);
        var effectiveBatchSize = Math.Clamp(batchSize, 1, 250);
        var normalizedNowUtc = NormalizeJourneyUtc(nowUtc) ?? DateTime.UtcNow;
        var candidates = new List<AdminKanbanJourneyStageAutomationCandidateRecord>(effectiveBatchSize);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (@batchSize)
    j.LeadId,
    j.Id,
    j.BoardType,
    lead.StageId,
    stage.Name,
    j.CurrentState,
    j.QualificationStatus,
    j.SchedulingStatus,
    j.CreatedAt,
    j.LastIntakeAt,
    COALESCE(stateEntry.CreatedAt, j.UpdatedAt, j.LastIntakeAt, j.CreatedAt) AS CurrentStateEnteredAtUtc,
    j.SuggestedAtUtc,
    j.ActiveTimerCode,
    j.ActiveTimerDueAtUtc,
    j.LastStageAutomationReason,
    j.LastStageAutomationOrigin
FROM dbo.{TablePrefix}journey_executions j
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = j.LeadId
INNER JOIN dbo.{TablePrefix}kanban_stages stage ON stage.Id = lead.StageId
OUTER APPLY
(
    SELECT TOP (1) evt.CreatedAt
    FROM dbo.{TablePrefix}journey_events evt
    WHERE evt.JourneyId = j.Id
      AND evt.ToState = j.CurrentState
    ORDER BY evt.CreatedAt DESC, evt.Id DESC
) stateEntry
WHERE lead.IsActive = 1
  AND j.BoardType = @boardType
ORDER BY
    CASE WHEN j.ActiveTimerDueAtUtc IS NOT NULL AND j.ActiveTimerDueAtUtc <= @nowUtc THEN 0 ELSE 1 END,
    COALESCE(j.ActiveTimerDueAtUtc, stateEntry.CreatedAt, j.UpdatedAt, j.LastIntakeAt, j.CreatedAt),
    j.Id;
""";
        command.Parameters.Add(new SqlParameter("@batchSize", SqlDbType.Int) { Value = effectiveBatchSize });
        command.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType });
        command.Parameters.Add(new SqlParameter("@nowUtc", SqlDbType.DateTime2) { Value = normalizedNowUtc });

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            candidates.Add(new AdminKanbanJourneyStageAutomationCandidateRecord
            {
                LeadId = reader.GetInt32(0),
                JourneyId = reader.GetInt32(1),
                BoardType = reader.GetString(2),
                StageId = reader.GetInt32(3),
                StageName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                CurrentState = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                QualificationStatus = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                SchedulingStatus = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                CreatedAtUtc = ReadAsUtcDateTime(reader, 8),
                LastIntakeAtUtc = ReadNullableUtcDateTime(reader, 9),
                CurrentStateEnteredAtUtc = ReadNullableUtcDateTime(reader, 10),
                SchedulingSuggestedAtUtc = ReadNullableUtcDateTime(reader, 11),
                ActiveTimerCode = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                ActiveTimerDueAtUtc = ReadNullableUtcDateTime(reader, 13),
                LastAutomationReason = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                LastAutomationOrigin = reader.IsDBNull(15) ? string.Empty : reader.GetString(15)
            });
        }

        return candidates;
    }

    public AdminKanbanJourneyStageAutomationUpdateResult? ApplyJourneyStageAutomation(
        AdminKanbanJourneyStageAutomationUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);
        var normalizedTargetState = AdminKanbanJourneyStates.Normalize(request.TargetCurrentState);
        var normalizedOrigin = AdminKanbanJourneyAutomationOrigins.Normalize(request.Origin);
        var normalizedReason = TrimToOrNull(request.Reason, 180) ?? string.Empty;
        var normalizedTimerCode = NormalizeJourneyTimerCode(request.ActiveTimerCode);
        var normalizedTimerDueAtUtc = NormalizeJourneyUtc(request.ActiveTimerDueAtUtc);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        JourneyAutomationExecutionMatch? match;
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
    j.ActiveTimerDueAtUtc
FROM dbo.{TablePrefix}journey_executions j
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = j.LeadId
INNER JOIN dbo.{TablePrefix}kanban_stages stage ON stage.Id = lead.StageId
WHERE j.LeadId = @leadId
  AND j.BoardType = @boardType
  AND lead.IsActive = 1
ORDER BY COALESCE(j.UpdatedAt, j.LastIntakeAt, j.CreatedAt) DESC, j.Id DESC;
""";
            command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = request.LeadId });
            command.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType });

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
        }

        var targetStageName = string.IsNullOrWhiteSpace(request.TargetStageName)
            ? match.StageName
            : TrimTo(request.TargetStageName, 120);
        var targetStageId = string.Equals(match.StageName, targetStageName, StringComparison.Ordinal)
            ? match.StageId
            : GetStageIdByName(connection, transaction, normalizedBoardType, targetStageName);
        var stageChanged = targetStageId != match.StageId;
        var stateChanged = !string.Equals(match.CurrentState, normalizedTargetState, StringComparison.OrdinalIgnoreCase);
        var reasonChanged = !string.Equals(match.LastAutomationReason, normalizedReason, StringComparison.Ordinal);
        var originChanged = !string.Equals(match.LastAutomationOrigin, normalizedOrigin, StringComparison.OrdinalIgnoreCase);
        var timerCodeChanged = !string.Equals(match.ActiveTimerCode, normalizedTimerCode, StringComparison.OrdinalIgnoreCase);
        var timerDueChanged = match.ActiveTimerDueAtUtc != normalizedTimerDueAtUtc;

        if (!stageChanged &&
            !stateChanged &&
            !reasonChanged &&
            !originChanged &&
            !timerCodeChanged &&
            !timerDueChanged)
        {
            transaction.Rollback();
            return new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = match.LeadId,
                JourneyId = match.JourneyId,
                FromStageId = match.StageId,
                FromStageName = match.StageName,
                ToStageId = match.StageId,
                ToStageName = match.StageName,
                CurrentState = match.CurrentState,
                StageChanged = false
            };
        }

        if (stageChanged)
        {
            using var updateLeadCommand = connection.CreateCommand();
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
            updateLeadCommand.Parameters.Add(new SqlParameter("@sortOrder", SqlDbType.Int) { Value = GetNextLeadSortOrder(connection, transaction, normalizedBoardType, targetStageId) });
            updateLeadCommand.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = match.LeadId });
            updateLeadCommand.ExecuteNonQuery();
        }

        using (var updateJourneyCommand = connection.CreateCommand())
        {
            updateJourneyCommand.Transaction = transaction;
            updateJourneyCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_executions
SET CurrentState = @currentState,
    LastStageAutomationReason = @lastStageAutomationReason,
    LastStageAutomationOrigin = @lastStageAutomationOrigin,
    LastStageAutomationAtUtc = SYSUTCDATETIME(),
    ActiveTimerCode = @activeTimerCode,
    ActiveTimerDueAtUtc = @activeTimerDueAtUtc,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @journeyId;
""";
            updateJourneyCommand.Parameters.Add(new SqlParameter("@journeyId", SqlDbType.Int) { Value = match.JourneyId });
            updateJourneyCommand.Parameters.Add(new SqlParameter("@currentState", SqlDbType.NVarChar, 40) { Value = normalizedTargetState });
            updateJourneyCommand.Parameters.Add(new SqlParameter("@lastStageAutomationReason", SqlDbType.NVarChar, 180) { Value = ToDbValue(normalizedReason) });
            updateJourneyCommand.Parameters.Add(new SqlParameter("@lastStageAutomationOrigin", SqlDbType.NVarChar, 40) { Value = ToDbValue(normalizedOrigin) });
            updateJourneyCommand.Parameters.Add(new SqlParameter("@activeTimerCode", SqlDbType.NVarChar, 60) { Value = ToDbValue(normalizedTimerCode) });
            updateJourneyCommand.Parameters.Add(new SqlParameter("@activeTimerDueAtUtc", SqlDbType.DateTime2) { Value = normalizedTimerDueAtUtc.HasValue ? normalizedTimerDueAtUtc.Value : DBNull.Value });
            updateJourneyCommand.ExecuteNonQuery();
        }

        if (!string.IsNullOrWhiteSpace(request.HistoryEventType) && !string.IsNullOrWhiteSpace(request.HistoryDescription))
        {
            InsertJourneyEventRecord(
                connection,
                transaction,
                match.JourneyId,
                match.LeadId,
                request.HistoryEventType,
                match.CurrentState,
                normalizedTargetState,
                string.IsNullOrWhiteSpace(match.SourceChannel) ? AdminKanbanJourneySourceChannels.ServiceRequest : match.SourceChannel,
                request.HistoryDescription,
                request.MetadataJson);

            InsertHistory(
                connection,
                transaction,
                match.LeadId,
                request.HistoryEventType,
                stageChanged ? match.StageId : null,
                stageChanged ? targetStageId : null,
                request.HistoryDescription);
        }

        transaction.Commit();

        return new AdminKanbanJourneyStageAutomationUpdateResult
        {
            LeadId = match.LeadId,
            JourneyId = match.JourneyId,
            FromStageId = match.StageId,
            FromStageName = match.StageName,
            ToStageId = targetStageId,
            ToStageName = targetStageName,
            CurrentState = normalizedTargetState,
            StageChanged = stageChanged
        };
    }

    private void EnsureJourneySchemaInitialized()
    {
        if (_journeyInitialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_journeyInitialized)
            {
                return;
            }

            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
IF OBJECT_ID('dbo.{TablePrefix}journey_executions', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}journey_executions
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    JourneyPublicId UNIQUEIDENTIFIER NOT NULL,
    LeadId INT NOT NULL,
    BoardType NVARCHAR(30) NOT NULL,
    JourneyKey NVARCHAR(140) NULL,
    SourceChannel NVARCHAR(40) NOT NULL,
    SourceOrigin NVARCHAR(160) NULL,
    CurrentState NVARCHAR(40) NOT NULL,
    LandingLeadId UNIQUEIDENTIFIER NULL,
    ServiceRequestId UNIQUEIDENTIFIER NULL,
    ClientId UNIQUEIDENTIFIER NULL,
    VisitorId NVARCHAR(80) NULL,
    SessionId NVARCHAR(80) NULL,
    ChatbotConversationId UNIQUEIDENTIFIER NULL,
    ChannelConversationId NVARCHAR(180) NULL,
    TelegramChatId BIGINT NULL,
    PrimaryPhone NVARCHAR(30) NULL,
    PrimaryEmail NVARCHAR(180) NULL,
    QualificationStatus NVARCHAR(40) NULL,
    QualificationSource NVARCHAR(40) NULL,
    QualificationConfidenceScore DECIMAL(5,2) NULL,
    QualificationHasRequiredData BIT NULL,
    QualificationNeedsConfirmation BIT NULL,
    QualificationSummary NVARCHAR(500) NULL,
    QualificationJson NVARCHAR(MAX) NULL,
    QualificationQualifiedAt DATETIME2 NULL,
    SchedulingStatus NVARCHAR(40) NULL,
    SchedulingSummary NVARCHAR(500) NULL,
    GoogleCalendarEventId NVARCHAR(180) NULL,
    GoogleCalendarEventLink NVARCHAR(500) NULL,
    SuggestedSlotsJson NVARCHAR(MAX) NULL,
    SuggestedAtUtc DATETIME2 NULL,
    SchedulingConfirmedAtUtc DATETIME2 NULL,
    SchedulingCancelledAtUtc DATETIME2 NULL,
    ScheduledStartAtUtc DATETIME2 NULL,
    ScheduledEndAtUtc DATETIME2 NULL,
    LastStageAutomationReason NVARCHAR(180) NULL,
    LastStageAutomationOrigin NVARCHAR(40) NULL,
    LastStageAutomationAtUtc DATETIME2 NULL,
    ActiveTimerCode NVARCHAR(60) NULL,
    ActiveTimerDueAtUtc DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL,
    LastIntakeAt DATETIME2 NULL
);

IF OBJECT_ID('dbo.{TablePrefix}journey_events', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}journey_events
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    JourneyId INT NOT NULL,
    LeadId INT NOT NULL,
    EventType NVARCHAR(60) NOT NULL,
    FromState NVARCHAR(40) NULL,
    ToState NVARCHAR(40) NULL,
    SourceChannel NVARCHAR(40) NULL,
    Description NVARCHAR(500) NULL,
    MetadataJson NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME())
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'UX_{TablePrefix}journey_executions_public_id')
CREATE UNIQUE INDEX UX_{TablePrefix}journey_executions_public_id
    ON dbo.{TablePrefix}journey_executions(JourneyPublicId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_lead')
CREATE INDEX IX_{TablePrefix}journey_executions_lead
    ON dbo.{TablePrefix}journey_executions(LeadId, UpdatedAt DESC, Id DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_service_request')
CREATE INDEX IX_{TablePrefix}journey_executions_service_request
    ON dbo.{TablePrefix}journey_executions(ServiceRequestId, UpdatedAt DESC, Id DESC)
    WHERE ServiceRequestId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_landing_lead')
CREATE INDEX IX_{TablePrefix}journey_executions_landing_lead
    ON dbo.{TablePrefix}journey_executions(LandingLeadId, UpdatedAt DESC, Id DESC)
    WHERE LandingLeadId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_chatbot_conversation')
CREATE INDEX IX_{TablePrefix}journey_executions_chatbot_conversation
    ON dbo.{TablePrefix}journey_executions(ChatbotConversationId, UpdatedAt DESC, Id DESC)
    WHERE ChatbotConversationId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_telegram_chat')
CREATE INDEX IX_{TablePrefix}journey_executions_telegram_chat
    ON dbo.{TablePrefix}journey_executions(TelegramChatId, UpdatedAt DESC, Id DESC)
    WHERE TelegramChatId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_phone')
CREATE INDEX IX_{TablePrefix}journey_executions_phone
    ON dbo.{TablePrefix}journey_executions(BoardType, PrimaryPhone, LastIntakeAt DESC, Id DESC)
    WHERE PrimaryPhone IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_email')
CREATE INDEX IX_{TablePrefix}journey_executions_email
    ON dbo.{TablePrefix}journey_executions(BoardType, PrimaryEmail, LastIntakeAt DESC, Id DESC)
    WHERE PrimaryEmail IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_executions') AND name = 'IX_{TablePrefix}journey_executions_active_timer')
CREATE INDEX IX_{TablePrefix}journey_executions_active_timer
    ON dbo.{TablePrefix}journey_executions(BoardType, ActiveTimerDueAtUtc, Id)
    WHERE ActiveTimerDueAtUtc IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_events') AND name = 'IX_{TablePrefix}journey_events_journey')
CREATE INDEX IX_{TablePrefix}journey_events_journey
    ON dbo.{TablePrefix}journey_events(JourneyId, CreatedAt DESC, Id DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}journey_events') AND name = 'IX_{TablePrefix}journey_events_lead')
CREATE INDEX IX_{TablePrefix}journey_events_lead
    ON dbo.{TablePrefix}journey_events(LeadId, CreatedAt DESC, Id DESC);

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationStatus') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationStatus NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationSource') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationSource NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationConfidenceScore') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationConfidenceScore DECIMAL(5,2) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationHasRequiredData') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationHasRequiredData BIT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationNeedsConfirmation') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationNeedsConfirmation BIT NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationSummary') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationSummary NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationJson') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationJson NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'QualificationQualifiedAt') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD QualificationQualifiedAt DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'SchedulingStatus') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD SchedulingStatus NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'SchedulingSummary') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD SchedulingSummary NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'GoogleCalendarEventId') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD GoogleCalendarEventId NVARCHAR(180) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'GoogleCalendarEventLink') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD GoogleCalendarEventLink NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'SuggestedSlotsJson') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD SuggestedSlotsJson NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'SuggestedAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD SuggestedAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'SchedulingConfirmedAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD SchedulingConfirmedAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'SchedulingCancelledAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD SchedulingCancelledAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ScheduledStartAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ScheduledStartAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ScheduledEndAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ScheduledEndAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'LastStageAutomationReason') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD LastStageAutomationReason NVARCHAR(180) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'LastStageAutomationOrigin') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD LastStageAutomationOrigin NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'LastStageAutomationAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD LastStageAutomationAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ActiveTimerCode') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ActiveTimerCode NVARCHAR(60) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ActiveTimerDueAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ActiveTimerDueAtUtc DATETIME2 NULL;
""";
            command.ExecuteNonQuery();
            transaction.Commit();
            _journeyInitialized = true;
        }
    }

    private JourneyExecutionMatch? TryFindJourneyMatch(
        SqlConnection connection,
        SqlTransaction transaction,
        string boardType,
        AdminKanbanJourneyIntakeRequest request,
        string? normalizedPhone,
        string? normalizedEmail,
        DateTime requestedAtUtc)
    {
        if (request.ServiceRequestId.HasValue)
        {
            var byServiceRequest = TryGetJourneyMatchByField(
                connection,
                transaction,
                boardType,
                "ServiceRequestId = @matchValue",
                new SqlParameter("@matchValue", SqlDbType.UniqueIdentifier) { Value = request.ServiceRequestId.Value });
            if (byServiceRequest is not null)
            {
                return byServiceRequest;
            }
        }

        if (request.LandingLeadId.HasValue)
        {
            var byLandingLead = TryGetJourneyMatchByField(
                connection,
                transaction,
                boardType,
                "LandingLeadId = @matchValue",
                new SqlParameter("@matchValue", SqlDbType.UniqueIdentifier) { Value = request.LandingLeadId.Value });
            if (byLandingLead is not null)
            {
                return byLandingLead;
            }
        }
        if (request.ChatbotConversationId.HasValue && request.ChatbotConversationId.Value != Guid.Empty)
        {
            var byConversation = TryGetJourneyMatchByField(
                connection,
                transaction,
                boardType,
                "ChatbotConversationId = @matchValue",
                new SqlParameter("@matchValue", SqlDbType.UniqueIdentifier) { Value = request.ChatbotConversationId.Value });
            if (byConversation is not null)
            {
                return byConversation;
            }
        }

        if (request.TelegramChatId.HasValue && request.TelegramChatId.Value > 0)
        {
            var byTelegramChat = TryGetJourneyMatchByField(
                connection,
                transaction,
                boardType,
                "TelegramChatId = @matchValue",
                new SqlParameter("@matchValue", SqlDbType.BigInt) { Value = request.TelegramChatId.Value });
            if (byTelegramChat is not null)
            {
                return byTelegramChat;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedPhone) || !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
SELECT TOP (1)
    j.Id,
    j.JourneyPublicId,
    j.LeadId,
    lead.StageId,
    j.SourceChannel,
    j.CurrentState,
    j.LandingLeadId,
    j.ServiceRequestId,
    j.LastIntakeAt,
    j.CreatedAt
FROM dbo.{TablePrefix}journey_executions j
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = j.LeadId
WHERE lead.IsActive = 1
  AND j.BoardType = @boardType
  AND COALESCE(j.LastIntakeAt, j.UpdatedAt, j.CreatedAt) >= @windowStart
  AND ((@phone IS NOT NULL AND j.PrimaryPhone = @phone)
    OR (@email IS NOT NULL AND j.PrimaryEmail = @email))
ORDER BY COALESCE(j.LastIntakeAt, j.UpdatedAt, j.CreatedAt) DESC, j.Id DESC;
""";
            command.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType });
            command.Parameters.Add(new SqlParameter("@windowStart", SqlDbType.DateTime2) { Value = requestedAtUtc.AddHours(-48) });
            command.Parameters.Add(new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = string.IsNullOrWhiteSpace(normalizedPhone) ? DBNull.Value : normalizedPhone });
            command.Parameters.Add(new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = string.IsNullOrWhiteSpace(normalizedEmail) ? DBNull.Value : normalizedEmail });

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadJourneyExecutionMatch(reader);
            }
        }

        return null;
    }

    private JourneyExecutionMatch? TryGetJourneyMatchByField(
        SqlConnection connection,
        SqlTransaction transaction,
        string boardType,
        string whereClause,
        SqlParameter parameter)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT TOP (1)
    j.Id,
    j.JourneyPublicId,
    j.LeadId,
    lead.StageId,
    j.SourceChannel,
    j.CurrentState,
    j.LandingLeadId,
    j.ServiceRequestId,
    j.LastIntakeAt,
    j.CreatedAt
FROM dbo.{TablePrefix}journey_executions j
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = j.LeadId
WHERE lead.IsActive = 1
  AND j.BoardType = @boardType
  AND {whereClause}
ORDER BY COALESCE(j.UpdatedAt, j.LastIntakeAt, j.CreatedAt) DESC, j.Id DESC;
""";
        command.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType });
        command.Parameters.Add(parameter);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadJourneyExecutionMatch(reader) : null;
    }

    private JourneyExecutionMatch? TryGetJourneyMatchByLeadId(
        SqlConnection connection,
        SqlTransaction transaction,
        int leadId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT TOP (1)
    j.Id,
    j.JourneyPublicId,
    j.LeadId,
    lead.StageId,
    j.SourceChannel,
    j.CurrentState,
    j.LandingLeadId,
    j.ServiceRequestId,
    j.LastIntakeAt,
    j.CreatedAt
FROM dbo.{TablePrefix}journey_executions j
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = j.LeadId
WHERE lead.IsActive = 1
  AND j.LeadId = @leadId
ORDER BY COALESCE(j.UpdatedAt, j.LastIntakeAt, j.CreatedAt) DESC, j.Id DESC;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadJourneyExecutionMatch(reader) : null;
    }

    private static JourneyExecutionMatch ReadJourneyExecutionMatch(SqlDataReader reader) => new()
    {
        JourneyId = reader.GetInt32(0),
        JourneyPublicId = reader.GetGuid(1),
        LeadId = reader.GetInt32(2),
        StageId = reader.GetInt32(3),
        SourceChannel = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
        CurrentState = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
        LandingLeadId = reader.IsDBNull(6) ? null : reader.GetGuid(6),
        ServiceRequestId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
        LastIntakeAt = ReadNullableUtcDateTime(reader, 8) ?? ReadAsUtcDateTime(reader, 9)
    };

    private int CreateJourneyLeadRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        string boardType,
        int stageId,
        AdminKanbanJourneyIntakeRequest request,
        string sourceChannel,
        string statusNote,
        DateTime requestedAtUtc)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}kanban_leads
(BoardType, StageId, SortOrder, Name, Phone, Email, ServiceCategory, PostalCode, City, Source, Priority, StatusNote, InternalNotes, LastContactAt, IsActive, CreatedAt, UpdatedAt)
VALUES
(@boardType, @stageId, @sortOrder, @name, @phone, @email, @serviceCategory, @postalCode, @city, @source, 'normal', @statusNote, @internalNotes, @lastContactAt, 1, SYSUTCDATETIME(), NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
            new SqlParameter("@stageId", SqlDbType.Int) { Value = stageId },
            new SqlParameter("@sortOrder", SqlDbType.Int) { Value = GetNextLeadSortOrder(connection, transaction, boardType, stageId) },
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = BuildJourneyLeadName(request, boardType) },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = ToDbValue(request.Phone) },
            new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = ToDbValue(request.Email) },
            new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = ToDbValue(request.ServiceCategory) },
            new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = ToDbValue(request.PostalCode) },
            new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = ToDbValue(request.City) },
            new SqlParameter("@source", SqlDbType.NVarChar, 120) { Value = BuildJourneyLeadSourceLabel(sourceChannel, isCrossChannelReentry: false) },
            new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = ToDbValue(statusNote) },
            new SqlParameter("@internalNotes", SqlDbType.NVarChar, -1) { Value = ToDbValue(BuildJourneyLeadInternalNotes(request)) },
            new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = NormalizeJourneyUtc(request.LastContactAtUtc) ?? requestedAtUtc }
        ]);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private void UpdateJourneyLeadRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        int leadId,
        AdminKanbanJourneyIntakeRequest request,
        string sourceChannel,
        string statusNote,
        DateTime requestedAtUtc,
        bool isCrossChannelReentry)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET Name = CASE WHEN NULLIF(@name, '') IS NOT NULL THEN @name ELSE Name END,
    Phone = CASE WHEN NULLIF(@phone, '') IS NOT NULL THEN @phone ELSE Phone END,
    Email = CASE WHEN NULLIF(@email, '') IS NOT NULL THEN @email ELSE Email END,
    ServiceCategory = CASE WHEN NULLIF(@serviceCategory, '') IS NOT NULL THEN @serviceCategory ELSE ServiceCategory END,
    PostalCode = CASE WHEN NULLIF(@postalCode, '') IS NOT NULL THEN @postalCode ELSE PostalCode END,
    City = CASE WHEN NULLIF(@city, '') IS NOT NULL THEN @city ELSE City END,
    Source = CASE WHEN NULLIF(@source, '') IS NOT NULL THEN @source ELSE Source END,
    StatusNote = CASE WHEN NULLIF(@statusNote, '') IS NOT NULL THEN @statusNote ELSE StatusNote END,
    InternalNotes = CASE WHEN NULLIF(@internalNotes, '') IS NOT NULL AND (InternalNotes IS NULL OR InternalNotes = '') THEN @internalNotes ELSE InternalNotes END,
    LastContactAt = COALESCE(@lastContactAt, LastContactAt),
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @leadId
  AND IsActive = 1;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = string.IsNullOrWhiteSpace(request.Name) ? string.Empty : TrimTo(request.Name, 140) },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = TrimTo(request.Phone, 30) },
            new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = TrimTo(request.Email, 180) },
            new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = TrimTo(request.ServiceCategory, 140) },
            new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = TrimTo(request.PostalCode, 9) },
            new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = TrimTo(request.City, 120) },
            new SqlParameter("@source", SqlDbType.NVarChar, 120) { Value = BuildJourneyLeadSourceLabel(sourceChannel, isCrossChannelReentry) },
            new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = TrimTo(statusNote, 500) },
            new SqlParameter("@internalNotes", SqlDbType.NVarChar, -1) { Value = ToDbValue(BuildJourneyLeadInternalNotes(request)) },
            new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = NormalizeJourneyUtc(request.LastContactAtUtc) ?? requestedAtUtc }
        ]);
        command.ExecuteNonQuery();
    }
    private (int JourneyId, Guid JourneyPublicId) CreateJourneyExecutionRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        int leadId,
        string boardType,
        AdminKanbanJourneyIntakeRequest request,
        string sourceChannel,
        string currentState,
        DateTime requestedAtUtc,
        string? normalizedPhone,
        string? normalizedEmail)
    {
        var publicId = Guid.NewGuid();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}journey_executions
(JourneyPublicId, LeadId, BoardType, JourneyKey, SourceChannel, SourceOrigin, CurrentState, LandingLeadId, ServiceRequestId, ClientId, VisitorId, SessionId, ChatbotConversationId, ChannelConversationId, TelegramChatId, PrimaryPhone, PrimaryEmail, QualificationStatus, QualificationSource, QualificationConfidenceScore, QualificationHasRequiredData, QualificationNeedsConfirmation, QualificationSummary, QualificationJson, QualificationQualifiedAt, SchedulingStatus, SchedulingSummary, GoogleCalendarEventId, GoogleCalendarEventLink, SuggestedSlotsJson, SuggestedAtUtc, SchedulingConfirmedAtUtc, SchedulingCancelledAtUtc, ScheduledStartAtUtc, ScheduledEndAtUtc, CreatedAt, UpdatedAt, LastIntakeAt)
VALUES
(@journeyPublicId, @leadId, @boardType, @journeyKey, @sourceChannel, @sourceOrigin, @currentState, @landingLeadId, @serviceRequestId, @clientId, @visitorId, @sessionId, @chatbotConversationId, @channelConversationId, @telegramChatId, @primaryPhone, @primaryEmail, @qualificationStatus, @qualificationSource, @qualificationConfidenceScore, @qualificationHasRequiredData, @qualificationNeedsConfirmation, @qualificationSummary, @qualificationJson, @qualificationQualifiedAt, @schedulingStatus, @schedulingSummary, @googleCalendarEventId, @googleCalendarEventLink, @suggestedSlotsJson, @suggestedAtUtc, @schedulingConfirmedAtUtc, @schedulingCancelledAtUtc, @scheduledStartAtUtc, @scheduledEndAtUtc, SYSUTCDATETIME(), SYSUTCDATETIME(), @lastIntakeAt);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@journeyPublicId", SqlDbType.UniqueIdentifier) { Value = publicId },
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
            new SqlParameter("@journeyKey", SqlDbType.NVarChar, 140) { Value = ToDbValue(BuildJourneyKey(boardType, request, normalizedPhone, normalizedEmail)) },
            new SqlParameter("@sourceChannel", SqlDbType.NVarChar, 40) { Value = sourceChannel },
            new SqlParameter("@sourceOrigin", SqlDbType.NVarChar, 160) { Value = ToDbValue(request.SourceOrigin) },
            new SqlParameter("@currentState", SqlDbType.NVarChar, 40) { Value = currentState },
            new SqlParameter("@landingLeadId", SqlDbType.UniqueIdentifier) { Value = request.LandingLeadId.HasValue ? request.LandingLeadId.Value : DBNull.Value },
            new SqlParameter("@serviceRequestId", SqlDbType.UniqueIdentifier) { Value = request.ServiceRequestId.HasValue ? request.ServiceRequestId.Value : DBNull.Value },
            new SqlParameter("@clientId", SqlDbType.UniqueIdentifier) { Value = request.ClientId.HasValue ? request.ClientId.Value : DBNull.Value },
            new SqlParameter("@visitorId", SqlDbType.NVarChar, 80) { Value = ToDbValue(TrimTo(request.VisitorId, 80)) },
            new SqlParameter("@sessionId", SqlDbType.NVarChar, 80) { Value = ToDbValue(TrimTo(request.SessionId, 80)) },
            new SqlParameter("@chatbotConversationId", SqlDbType.UniqueIdentifier) { Value = request.ChatbotConversationId.HasValue ? request.ChatbotConversationId.Value : DBNull.Value },
            new SqlParameter("@channelConversationId", SqlDbType.NVarChar, 180) { Value = ToDbValue(TrimTo(request.ChannelConversationId, 180)) },
            new SqlParameter("@telegramChatId", SqlDbType.BigInt) { Value = request.TelegramChatId.HasValue ? request.TelegramChatId.Value : DBNull.Value },
            new SqlParameter("@primaryPhone", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedPhone) },
            new SqlParameter("@primaryEmail", SqlDbType.NVarChar, 180) { Value = ToDbValue(normalizedEmail) },
            new SqlParameter("@qualificationStatus", SqlDbType.NVarChar, 40) { Value = ToDbValue(NormalizeJourneyQualificationStatus(request.Qualification.Status)) },
            new SqlParameter("@qualificationSource", SqlDbType.NVarChar, 40) { Value = ToDbValue(NormalizeJourneyQualificationSource(request.Qualification.Source)) },
            new SqlParameter("@qualificationConfidenceScore", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = request.Qualification.ConfidenceScore > 0 ? request.Qualification.ConfidenceScore : DBNull.Value },
            new SqlParameter("@qualificationHasRequiredData", SqlDbType.Bit) { Value = HasJourneyQualification(request.Qualification) ? request.Qualification.HasRequiredData : DBNull.Value },
            new SqlParameter("@qualificationNeedsConfirmation", SqlDbType.Bit) { Value = HasJourneyQualification(request.Qualification) ? request.Qualification.NeedsConfirmation : DBNull.Value },
            new SqlParameter("@qualificationSummary", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(request.Qualification.Summary, 500)) },
            new SqlParameter("@qualificationJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneyQualification(request.Qualification)) },
            new SqlParameter("@qualificationQualifiedAt", SqlDbType.DateTime2) { Value = request.Qualification.QualifiedAtUtc.HasValue ? NormalizeJourneyUtc(request.Qualification.QualifiedAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@schedulingStatus", SqlDbType.NVarChar, 40) { Value = ToDbValue(NormalizeJourneySchedulingStatus(request.Scheduling.Status)) },
            new SqlParameter("@schedulingSummary", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(request.Scheduling.Summary, 500)) },
            new SqlParameter("@googleCalendarEventId", SqlDbType.NVarChar, 180) { Value = ToDbValue(TrimToOrNull(request.Scheduling.GoogleCalendarEventId, 180)) },
            new SqlParameter("@googleCalendarEventLink", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(request.Scheduling.GoogleCalendarEventLink, 500)) },
            new SqlParameter("@suggestedSlotsJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneySchedulingSlots(request.Scheduling.SuggestedSlots)) },
            new SqlParameter("@suggestedAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.SuggestedAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.SuggestedAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@schedulingConfirmedAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.ConfirmedAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.ConfirmedAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@schedulingCancelledAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.CancelledAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.CancelledAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@scheduledStartAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.ScheduledStartAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.ScheduledStartAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@scheduledEndAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.ScheduledEndAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.ScheduledEndAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@lastIntakeAt", SqlDbType.DateTime2) { Value = requestedAtUtc }
        ]);

        return (Convert.ToInt32(command.ExecuteScalar()), publicId);
    }

    private (int JourneyId, Guid JourneyPublicId) UpdateJourneyExecutionRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        JourneyExecutionMatch match,
        AdminKanbanJourneyIntakeRequest request,
        string sourceChannel,
        string currentState,
        DateTime requestedAtUtc,
        string? normalizedPhone,
        string? normalizedEmail)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_executions
SET JourneyKey = COALESCE(NULLIF(@journeyKey, ''), JourneyKey),
    SourceOrigin = COALESCE(NULLIF(@sourceOrigin, ''), SourceOrigin),
    CurrentState = @currentState,
    LandingLeadId = COALESCE(@landingLeadId, LandingLeadId),
    ServiceRequestId = COALESCE(@serviceRequestId, ServiceRequestId),
    ClientId = COALESCE(@clientId, ClientId),
    VisitorId = COALESCE(NULLIF(@visitorId, ''), VisitorId),
    SessionId = COALESCE(NULLIF(@sessionId, ''), SessionId),
    ChatbotConversationId = COALESCE(@chatbotConversationId, ChatbotConversationId),
    ChannelConversationId = COALESCE(NULLIF(@channelConversationId, ''), ChannelConversationId),
    TelegramChatId = COALESCE(@telegramChatId, TelegramChatId),
    PrimaryPhone = COALESCE(NULLIF(@primaryPhone, ''), PrimaryPhone),
    PrimaryEmail = COALESCE(NULLIF(@primaryEmail, ''), PrimaryEmail),
    QualificationStatus = COALESCE(NULLIF(@qualificationStatus, ''), QualificationStatus),
    QualificationSource = COALESCE(NULLIF(@qualificationSource, ''), QualificationSource),
    QualificationConfidenceScore = COALESCE(@qualificationConfidenceScore, QualificationConfidenceScore),
    QualificationHasRequiredData = COALESCE(@qualificationHasRequiredData, QualificationHasRequiredData),
    QualificationNeedsConfirmation = COALESCE(@qualificationNeedsConfirmation, QualificationNeedsConfirmation),
    QualificationSummary = COALESCE(NULLIF(@qualificationSummary, ''), QualificationSummary),
    QualificationJson = COALESCE(NULLIF(@qualificationJson, ''), QualificationJson),
    QualificationQualifiedAt = COALESCE(@qualificationQualifiedAt, QualificationQualifiedAt),
    SchedulingStatus = COALESCE(NULLIF(@schedulingStatus, ''), SchedulingStatus),
    SchedulingSummary = COALESCE(NULLIF(@schedulingSummary, ''), SchedulingSummary),
    GoogleCalendarEventId = COALESCE(NULLIF(@googleCalendarEventId, ''), GoogleCalendarEventId),
    GoogleCalendarEventLink = COALESCE(NULLIF(@googleCalendarEventLink, ''), GoogleCalendarEventLink),
    SuggestedSlotsJson = COALESCE(NULLIF(@suggestedSlotsJson, ''), SuggestedSlotsJson),
    SuggestedAtUtc = COALESCE(@suggestedAtUtc, SuggestedAtUtc),
    SchedulingConfirmedAtUtc = COALESCE(@schedulingConfirmedAtUtc, SchedulingConfirmedAtUtc),
    SchedulingCancelledAtUtc = COALESCE(@schedulingCancelledAtUtc, SchedulingCancelledAtUtc),
    ScheduledStartAtUtc = COALESCE(@scheduledStartAtUtc, ScheduledStartAtUtc),
    ScheduledEndAtUtc = COALESCE(@scheduledEndAtUtc, ScheduledEndAtUtc),
    UpdatedAt = SYSUTCDATETIME(),
    LastIntakeAt = @lastIntakeAt
WHERE Id = @journeyId;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@journeyId", SqlDbType.Int) { Value = match.JourneyId },
            new SqlParameter("@journeyKey", SqlDbType.NVarChar, 140) { Value = ToDbValue(BuildJourneyKey(AdminKanbanBoardTypes.Normalize(request.BoardType), request, normalizedPhone, normalizedEmail)) },
            new SqlParameter("@sourceOrigin", SqlDbType.NVarChar, 160) { Value = ToDbValue(request.SourceOrigin) },
            new SqlParameter("@currentState", SqlDbType.NVarChar, 40) { Value = currentState },
            new SqlParameter("@landingLeadId", SqlDbType.UniqueIdentifier) { Value = request.LandingLeadId.HasValue ? request.LandingLeadId.Value : DBNull.Value },
            new SqlParameter("@serviceRequestId", SqlDbType.UniqueIdentifier) { Value = request.ServiceRequestId.HasValue ? request.ServiceRequestId.Value : DBNull.Value },
            new SqlParameter("@clientId", SqlDbType.UniqueIdentifier) { Value = request.ClientId.HasValue ? request.ClientId.Value : DBNull.Value },
            new SqlParameter("@visitorId", SqlDbType.NVarChar, 80) { Value = ToDbValue(TrimTo(request.VisitorId, 80)) },
            new SqlParameter("@sessionId", SqlDbType.NVarChar, 80) { Value = ToDbValue(TrimTo(request.SessionId, 80)) },
            new SqlParameter("@chatbotConversationId", SqlDbType.UniqueIdentifier) { Value = request.ChatbotConversationId.HasValue ? request.ChatbotConversationId.Value : DBNull.Value },
            new SqlParameter("@channelConversationId", SqlDbType.NVarChar, 180) { Value = ToDbValue(TrimTo(request.ChannelConversationId, 180)) },
            new SqlParameter("@telegramChatId", SqlDbType.BigInt) { Value = request.TelegramChatId.HasValue ? request.TelegramChatId.Value : DBNull.Value },
            new SqlParameter("@primaryPhone", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedPhone) },
            new SqlParameter("@primaryEmail", SqlDbType.NVarChar, 180) { Value = ToDbValue(normalizedEmail) },
            new SqlParameter("@qualificationStatus", SqlDbType.NVarChar, 40) { Value = ToDbValue(NormalizeJourneyQualificationStatus(request.Qualification.Status)) },
            new SqlParameter("@qualificationSource", SqlDbType.NVarChar, 40) { Value = ToDbValue(NormalizeJourneyQualificationSource(request.Qualification.Source)) },
            new SqlParameter("@qualificationConfidenceScore", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = request.Qualification.ConfidenceScore > 0 ? request.Qualification.ConfidenceScore : DBNull.Value },
            new SqlParameter("@qualificationHasRequiredData", SqlDbType.Bit) { Value = HasJourneyQualification(request.Qualification) ? request.Qualification.HasRequiredData : DBNull.Value },
            new SqlParameter("@qualificationNeedsConfirmation", SqlDbType.Bit) { Value = HasJourneyQualification(request.Qualification) ? request.Qualification.NeedsConfirmation : DBNull.Value },
            new SqlParameter("@qualificationSummary", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(request.Qualification.Summary, 500)) },
            new SqlParameter("@qualificationJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneyQualification(request.Qualification)) },
            new SqlParameter("@qualificationQualifiedAt", SqlDbType.DateTime2) { Value = request.Qualification.QualifiedAtUtc.HasValue ? NormalizeJourneyUtc(request.Qualification.QualifiedAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@schedulingStatus", SqlDbType.NVarChar, 40) { Value = ToDbValue(NormalizeJourneySchedulingStatus(request.Scheduling.Status)) },
            new SqlParameter("@schedulingSummary", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(request.Scheduling.Summary, 500)) },
            new SqlParameter("@googleCalendarEventId", SqlDbType.NVarChar, 180) { Value = ToDbValue(TrimToOrNull(request.Scheduling.GoogleCalendarEventId, 180)) },
            new SqlParameter("@googleCalendarEventLink", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(request.Scheduling.GoogleCalendarEventLink, 500)) },
            new SqlParameter("@suggestedSlotsJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneySchedulingSlots(request.Scheduling.SuggestedSlots)) },
            new SqlParameter("@suggestedAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.SuggestedAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.SuggestedAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@schedulingConfirmedAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.ConfirmedAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.ConfirmedAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@schedulingCancelledAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.CancelledAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.CancelledAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@scheduledStartAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.ScheduledStartAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.ScheduledStartAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@scheduledEndAtUtc", SqlDbType.DateTime2) { Value = request.Scheduling.ScheduledEndAtUtc.HasValue ? NormalizeJourneyUtc(request.Scheduling.ScheduledEndAtUtc)!.Value : DBNull.Value },
            new SqlParameter("@lastIntakeAt", SqlDbType.DateTime2) { Value = requestedAtUtc }
        ]);
        command.ExecuteNonQuery();
        return (match.JourneyId, match.JourneyPublicId);
    }

    private void InsertJourneyEventRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        int journeyId,
        int leadId,
        string eventType,
        string? fromState,
        string toState,
        string sourceChannel,
        string description,
        string metadataJson)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}journey_events
(JourneyId, LeadId, EventType, FromState, ToState, SourceChannel, Description, MetadataJson, CreatedAt)
VALUES
(@journeyId, @leadId, @eventType, @fromState, @toState, @sourceChannel, @description, @metadataJson, SYSUTCDATETIME());
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@journeyId", SqlDbType.Int) { Value = journeyId },
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
            new SqlParameter("@eventType", SqlDbType.NVarChar, 60) { Value = TrimTo(eventType, 60) },
            new SqlParameter("@fromState", SqlDbType.NVarChar, 40) { Value = ToDbValue(fromState) },
            new SqlParameter("@toState", SqlDbType.NVarChar, 40) { Value = TrimTo(toState, 40) },
            new SqlParameter("@sourceChannel", SqlDbType.NVarChar, 40) { Value = TrimTo(sourceChannel, 40) },
            new SqlParameter("@description", SqlDbType.NVarChar, 500) { Value = TrimTo(description, 500) },
            new SqlParameter("@metadataJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(metadataJson) }
        ]);
        command.ExecuteNonQuery();
    }

    private static string ResolveJourneyState(AdminKanbanJourneyIntakeRequest request, string? currentState)
    {
        var targetState = request.ServiceRequestId.HasValue ||
                          string.Equals(request.SourceChannel, AdminKanbanJourneySourceChannels.ServiceRequest, StringComparison.OrdinalIgnoreCase)
            ? AdminKanbanJourneyStates.ServiceRequestOpened
            : ResolveJourneyStateFromQualification(request);

        var normalizedCurrent = string.IsNullOrWhiteSpace(currentState)
            ? AdminKanbanJourneyStates.IntakeOpened
            : AdminKanbanJourneyStates.Normalize(currentState);

        return AdminKanbanJourneyStates.GetSortOrder(normalizedCurrent) > AdminKanbanJourneyStates.GetSortOrder(targetState)
            ? normalizedCurrent
            : targetState;
    }

    private static string ResolveJourneyUpdateEventType(
        JourneyExecutionMatch? match,
        string sourceChannel,
        AdminKanbanJourneyIntakeRequest request,
        string currentState,
        bool isCrossChannelReentry)
    {
        if (isCrossChannelReentry)
        {
            return "jornada_reentrada_omnichannel";
        }

        if (request.ServiceRequestId.HasValue && !(match?.ServiceRequestId.HasValue ?? false))
        {
            return "jornada_pedido_vinculado";
        }

        return currentState == AdminKanbanJourneyStates.ServiceRequestOpened
            ? "jornada_atualizada"
            : string.Equals(sourceChannel, AdminKanbanJourneySourceChannels.Telegram, StringComparison.OrdinalIgnoreCase)
                ? "jornada_atualizada"
                : "jornada_atualizada";
    }
    private static string BuildJourneyStatusNote(
        AdminKanbanJourneyIntakeRequest request,
        string sourceChannel,
        string currentState,
        bool alreadyExists)
    {
        var prefix = alreadyExists ? "Jornada atualizada" : "Jornada iniciada";
        var channelLabel = AdminKanbanJourneySourceChannels.GetLabel(sourceChannel);
        var stateLabel = AdminKanbanJourneyStates.GetLabel(currentState);
        var suffix = request.ServiceRequestId.HasValue ? " Pedido de servico vinculado." : string.Empty;
        return TrimTo($"{prefix} via {channelLabel}. Estado atual: {stateLabel}.{suffix}", 500);
    }

    private static string BuildJourneyHistoryDescription(
        string sourceChannel,
        string currentState,
        bool createdLead,
        bool isCrossChannelReentry,
        bool serviceRequestLinked)
    {
        var channelLabel = AdminKanbanJourneySourceChannels.GetLabel(sourceChannel);
        var stateLabel = AdminKanbanJourneyStates.GetLabel(currentState);

        if (createdLead)
        {
            return $"Jornada automatica iniciada via {channelLabel}. Estado atual: {stateLabel}.";
        }

        if (isCrossChannelReentry)
        {
            return $"Jornada retomada via {channelLabel}, reaproveitando o mesmo lead. Estado atual: {stateLabel}.";
        }

        if (serviceRequestLinked)
        {
            return $"Pedido de servico vinculado a jornada por {channelLabel}. Estado atual: {stateLabel}.";
        }

        return $"Jornada automatica atualizada via {channelLabel}. Estado atual: {stateLabel}.";
    }

    private static string BuildJourneyLeadName(AdminKanbanJourneyIntakeRequest request, string? boardType)
    {
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            return TrimTo(request.Name, 140);
        }

        return string.Equals(boardType, AdminKanbanBoardTypes.Providers, StringComparison.OrdinalIgnoreCase)
            ? "Prestador sem identificacao"
            : "Cliente sem identificacao";
    }

    private static string BuildJourneyLeadSourceLabel(string sourceChannel, bool isCrossChannelReentry) =>
        isCrossChannelReentry
            ? "Omnichannel"
            : AdminKanbanJourneySourceChannels.GetLabel(sourceChannel);

    private static string? BuildJourneyLeadInternalNotes(AdminKanbanJourneyIntakeRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.InternalNotes))
        {
            parts.Add(request.InternalNotes.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.SourceOrigin))
        {
            parts.Add($"Origem tecnica: {TrimTo(request.SourceOrigin, 160)}");
        }

        if (!string.IsNullOrWhiteSpace(request.VisitorId))
        {
            parts.Add($"VisitorId: {TrimTo(request.VisitorId, 80)}");
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            parts.Add($"SessionId: {TrimTo(request.SessionId, 80)}");
        }

        if (!string.IsNullOrWhiteSpace(request.ProblemDescription))
        {
            parts.Add($"Contexto inicial: {TrimTo(request.ProblemDescription.Trim(), 500)}");
        }

        var addressParts = new[]
        {
            TrimToOrNull(request.Street, 180),
            TrimToOrNull(request.Neighborhood, 120),
            TrimToOrNull(request.City, 120),
            TrimToOrNull(request.State, 2),
            TrimToOrNull(request.PostalCode, 9)
        }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

        if (addressParts.Length > 0)
        {
            parts.Add($"Endereco inicial: {string.Join(", ", addressParts)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Qualification.Summary))
        {
            parts.Add($"Resumo da qualificacao: {TrimTo(request.Qualification.Summary.Trim(), 500)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Qualification.ConfirmationPrompt))
        {
            parts.Add($"Confirmacao solicitada: {TrimTo(request.Qualification.ConfirmationPrompt.Trim(), 500)}");
        }

        return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
    }

    private static string BuildJourneyKey(string boardType, AdminKanbanJourneyIntakeRequest request, string? normalizedPhone, string? normalizedEmail)
    {
        if (request.ServiceRequestId.HasValue)
        {
            return $"{boardType}:service-request:{request.ServiceRequestId.Value:N}";
        }

        if (request.LandingLeadId.HasValue)
        {
            return $"{boardType}:landing:{request.LandingLeadId.Value:N}";
        }

        if (request.ChatbotConversationId.HasValue && request.ChatbotConversationId.Value != Guid.Empty)
        {
            return $"{boardType}:telegram-conversation:{request.ChatbotConversationId.Value:N}";
        }

        if (request.TelegramChatId.HasValue && request.TelegramChatId.Value > 0)
        {
            return $"{boardType}:telegram-chat:{request.TelegramChatId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return $"{boardType}:phone:{normalizedPhone}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return $"{boardType}:email:{normalizedEmail}";
        }

        return $"{boardType}:journey:{Guid.NewGuid():N}";
    }

    private static string BuildJourneyEventMetadataJson(AdminKanbanJourneyIntakeRequest request, string? normalizedPhone, string? normalizedEmail)
    {
        var payload = new
        {
            request.BoardType,
            request.SourceChannel,
            request.SourceOrigin,
            request.ServiceCategory,
            request.ProblemDescription,
            request.Street,
            request.Neighborhood,
            request.State,
            request.PostalCode,
            request.City,
            request.Latitude,
            request.Longitude,
            request.LandingLeadId,
            request.ServiceRequestId,
            request.ClientId,
            request.VisitorId,
            request.SessionId,
            request.ChatbotConversationId,
            request.ChannelConversationId,
            request.TelegramChatId,
            Qualification = request.Qualification,
            normalizedPhone,
            normalizedEmail,
            RequestedAtUtc = NormalizeJourneyUtc(request.RequestedAtUtc),
            LastContactAtUtc = NormalizeJourneyUtc(request.LastContactAtUtc)
        };

        return JsonSerializer.Serialize(payload);
    }

    private static DateTime? NormalizeJourneyUtc(DateTime? value)
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

    private static string? NormalizeJourneyPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : TrimTo(digits, 30);
    }

    private static string? NormalizeJourneyEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TrimTo(value.Trim().ToLowerInvariant(), 180);
    }

    private static string ResolveJourneyStateFromQualification(AdminKanbanJourneyIntakeRequest request)
    {
        var normalizedStatus = NormalizeJourneyQualificationStatus(request.Qualification.Status);
        return normalizedStatus switch
        {
            AdminKanbanJourneyQualificationStatuses.Pending => AdminKanbanJourneyStates.QualificationPending,
            AdminKanbanJourneyQualificationStatuses.ConfirmationRequired => AdminKanbanJourneyStates.QualificationConfirmationRequired,
            AdminKanbanJourneyQualificationStatuses.Qualified => AdminKanbanJourneyStates.QualificationValidated,
            _ when string.Equals(request.SourceChannel, AdminKanbanJourneySourceChannels.Telegram, StringComparison.OrdinalIgnoreCase)
                => AdminKanbanJourneyStates.AutomatedTriage,
            _ => AdminKanbanJourneyStates.IntakeOpened
        };
    }

    private static string NormalizeJourneyQualificationStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AdminKanbanJourneyQualificationStatuses.Normalize(value);

    private static string NormalizeJourneyQualificationSource(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AdminKanbanJourneyQualificationSources.Normalize(value);

    private static string NormalizeJourneySchedulingStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AdminKanbanJourneySchedulingStatuses.Normalize(value);

    private static string NormalizeJourneyTimerCode(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        AdminKanbanJourneyTimerCodes.PendingData => AdminKanbanJourneyTimerCodes.PendingData,
        AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation => AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation,
        AdminKanbanJourneyTimerCodes.PendingAcceptance => AdminKanbanJourneyTimerCodes.PendingAcceptance,
        AdminKanbanJourneyTimerCodes.PendingClientReview => AdminKanbanJourneyTimerCodes.PendingClientReview,
        AdminKanbanJourneyTimerCodes.PendingProviderReview => AdminKanbanJourneyTimerCodes.PendingProviderReview,
        _ => string.Empty
    };

    private static bool HasJourneyQualification(AdminKanbanJourneyQualificationRecord qualification) =>
        !string.IsNullOrWhiteSpace(qualification.Status) ||
        !string.IsNullOrWhiteSpace(qualification.Source) ||
        !string.IsNullOrWhiteSpace(qualification.NormalizedServiceCategoryName) ||
        !string.IsNullOrWhiteSpace(qualification.ProblemContext) ||
        !string.IsNullOrWhiteSpace(qualification.City) ||
        !string.IsNullOrWhiteSpace(qualification.PostalCode) ||
        !string.IsNullOrWhiteSpace(qualification.Summary);

    private static string? SerializeJourneyQualification(AdminKanbanJourneyQualificationRecord qualification) =>
        HasJourneyQualification(qualification)
            ? JsonSerializer.Serialize(qualification)
            : null;

    private static string? SerializeJourneySchedulingSlots(IReadOnlyList<AdminKanbanJourneySuggestedSlotRecord>? suggestedSlots)
    {
        if (suggestedSlots is null || suggestedSlots.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(
            suggestedSlots
                .OrderBy(item => item.OptionNumber)
                .Select(item => new AdminKanbanJourneySuggestedSlotRecord
                {
                    OptionNumber = item.OptionNumber,
                    StartsAtUtc = NormalizeJourneyUtc(item.StartsAtUtc) ?? item.StartsAtUtc,
                    EndsAtUtc = NormalizeJourneyUtc(item.EndsAtUtc) ?? item.EndsAtUtc,
                    Label = TrimTo(item.Label, 160)
                })
                .ToList());
    }

    private static AdminKanbanJourneyQualificationRecord ReadJourneyQualificationRecord(SqlDataReader reader, int startIndex)
    {
        var status = reader.IsDBNull(startIndex) ? string.Empty : reader.GetString(startIndex);
        var source = reader.IsDBNull(startIndex + 1) ? string.Empty : reader.GetString(startIndex + 1);
        var confidenceScore = reader.IsDBNull(startIndex + 2) ? 0m : reader.GetDecimal(startIndex + 2);
        var hasRequiredData = !reader.IsDBNull(startIndex + 3) && reader.GetBoolean(startIndex + 3);
        var needsConfirmation = !reader.IsDBNull(startIndex + 4) && reader.GetBoolean(startIndex + 4);
        var summary = reader.IsDBNull(startIndex + 5) ? string.Empty : reader.GetString(startIndex + 5);
        var qualificationJson = reader.IsDBNull(startIndex + 6) ? string.Empty : reader.GetString(startIndex + 6);
        var qualifiedAtUtc = ReadNullableUtcDateTime(reader, startIndex + 7);

        if (!string.IsNullOrWhiteSpace(qualificationJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<AdminKanbanJourneyQualificationRecord>(qualificationJson);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        return new AdminKanbanJourneyQualificationRecord
        {
            Status = NormalizeJourneyQualificationStatus(status),
            Source = NormalizeJourneyQualificationSource(source),
            ConfidenceScore = confidenceScore,
            HasRequiredData = hasRequiredData,
            NeedsConfirmation = needsConfirmation,
            Summary = summary,
            QualifiedAtUtc = qualifiedAtUtc
        };
    }

    private static AdminKanbanJourneySchedulingRecord ReadJourneySchedulingRecord(SqlDataReader reader, int startIndex)
    {
        var status = reader.IsDBNull(startIndex) ? string.Empty : reader.GetString(startIndex);
        var summary = reader.IsDBNull(startIndex + 1) ? string.Empty : reader.GetString(startIndex + 1);
        var googleCalendarEventId = reader.IsDBNull(startIndex + 2) ? string.Empty : reader.GetString(startIndex + 2);
        var googleCalendarEventLink = reader.IsDBNull(startIndex + 3) ? string.Empty : reader.GetString(startIndex + 3);
        var suggestedSlotsJson = reader.IsDBNull(startIndex + 4) ? string.Empty : reader.GetString(startIndex + 4);
        var suggestedAtUtc = ReadNullableUtcDateTime(reader, startIndex + 5);
        var confirmedAtUtc = ReadNullableUtcDateTime(reader, startIndex + 6);
        var cancelledAtUtc = ReadNullableUtcDateTime(reader, startIndex + 7);
        var scheduledStartAtUtc = ReadNullableUtcDateTime(reader, startIndex + 8);
        var scheduledEndAtUtc = ReadNullableUtcDateTime(reader, startIndex + 9);

        return new AdminKanbanJourneySchedulingRecord
        {
            Status = NormalizeJourneySchedulingStatus(status),
            Summary = summary,
            GoogleCalendarEventId = googleCalendarEventId,
            GoogleCalendarEventLink = googleCalendarEventLink,
            SuggestedSlots = DeserializeJourneySchedulingSlots(suggestedSlotsJson),
            SuggestedAtUtc = suggestedAtUtc,
            ConfirmedAtUtc = confirmedAtUtc,
            CancelledAtUtc = cancelledAtUtc,
            ScheduledStartAtUtc = scheduledStartAtUtc,
            ScheduledEndAtUtc = scheduledEndAtUtc
        };
    }

    private static IReadOnlyList<AdminKanbanJourneySuggestedSlotRecord> DeserializeJourneySchedulingSlots(string? suggestedSlotsJson)
    {
        if (string.IsNullOrWhiteSpace(suggestedSlotsJson))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<AdminKanbanJourneySuggestedSlotRecord>>(suggestedSlotsJson);
            return parsed?
                .Where(item => item.OptionNumber > 0)
                .OrderBy(item => item.OptionNumber)
                .Select(item => new AdminKanbanJourneySuggestedSlotRecord
                {
                    OptionNumber = item.OptionNumber,
                    StartsAtUtc = NormalizeJourneyUtc(item.StartsAtUtc) ?? item.StartsAtUtc,
                    EndsAtUtc = NormalizeJourneyUtc(item.EndsAtUtc) ?? item.EndsAtUtc,
                    Label = TrimTo(item.Label, 160)
                })
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? TrimToOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed class JourneyExecutionMatch
    {
        public int JourneyId { get; init; }
        public Guid JourneyPublicId { get; init; }
        public int LeadId { get; init; }
        public int StageId { get; init; }
        public string SourceChannel { get; init; } = string.Empty;
        public string CurrentState { get; init; } = string.Empty;
        public Guid? LandingLeadId { get; init; }
        public Guid? ServiceRequestId { get; init; }
        public DateTime? LastIntakeAt { get; init; }
    }

    private sealed class JourneyAutomationExecutionMatch
    {
        public int JourneyId { get; init; }
        public int LeadId { get; init; }
        public string BoardType { get; init; } = string.Empty;
        public string SourceChannel { get; init; } = string.Empty;
        public string CurrentState { get; init; } = string.Empty;
        public int StageId { get; init; }
        public string StageName { get; init; } = string.Empty;
        public string LastAutomationReason { get; init; } = string.Empty;
        public string LastAutomationOrigin { get; init; } = string.Empty;
        public string ActiveTimerCode { get; init; } = string.Empty;
        public DateTime? ActiveTimerDueAtUtc { get; init; }
    }
}
