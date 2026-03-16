using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed partial class SqlAdminKanbanService
{
    public AdminKanbanJourneyClosureUpdateResult? UpdateJourneyClosure(int leadId, AdminKanbanJourneyClosureUpdateRequest request)
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
        var normalizedSourceChannel = string.IsNullOrWhiteSpace(request.SourceChannel)
            ? string.IsNullOrWhiteSpace(match.SourceChannel)
                ? AdminKanbanJourneySourceChannels.Telegram
                : AdminKanbanJourneySourceChannels.Normalize(match.SourceChannel)
            : AdminKanbanJourneySourceChannels.Normalize(request.SourceChannel);

        var closureRecord = new AdminKanbanJourneyClosureRecord
        {
            Status = NormalizeJourneyClosureStatus(request.Status),
            Summary = TrimToOrNull(request.Summary, 500) ?? string.Empty,
            Outcome = NormalizeJourneyClosureOutcome(request.Outcome),
            ServiceInProgressAtUtc = NormalizeJourneyUtc(request.ServiceInProgressAtUtc),
            ProviderCompletionRequestedAtUtc = NormalizeJourneyUtc(request.ProviderCompletionRequestedAtUtc),
            ProviderCompletionSubmittedAtUtc = NormalizeJourneyUtc(request.ProviderCompletionSubmittedAtUtc),
            ClientConfirmationRequestedAtUtc = NormalizeJourneyUtc(request.ClientConfirmationRequestedAtUtc),
            ClientConfirmedAtUtc = NormalizeJourneyUtc(request.ClientConfirmedAtUtc),
            CompletedAtUtc = NormalizeJourneyUtc(request.CompletedAtUtc),
            ContestedAtUtc = NormalizeJourneyUtc(request.ContestedAtUtc),
            ContestedReason = TrimToOrNull(request.ContestedReason, 500) ?? string.Empty,
            ClientReviewStatus = NormalizeJourneyReviewStatus(request.ClientReviewStatus),
            ProviderReviewStatus = NormalizeJourneyReviewStatus(request.ProviderReviewStatus),
            ClientReview = SanitizeJourneyReviewRecord(request.ClientReview),
            ProviderReview = SanitizeJourneyReviewRecord(request.ProviderReview)
        };

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
UPDATE dbo.{TablePrefix}journey_executions
SET CurrentState = @currentState,
    ClosureStatus = @closureStatus,
    ClosureSummary = @closureSummary,
    ClosureOutcome = @closureOutcome,
    ClosureCompletedAtUtc = @closureCompletedAtUtc,
    ClosureJson = @closureJson,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @journeyId;
""";
            command.Parameters.AddRange(
            [
                new SqlParameter("@journeyId", SqlDbType.Int) { Value = match.JourneyId },
                new SqlParameter("@currentState", SqlDbType.NVarChar, 40) { Value = normalizedCurrentState },
                new SqlParameter("@closureStatus", SqlDbType.NVarChar, 40) { Value = ToDbValue(TrimToOrNull(closureRecord.Status, 40)) },
                new SqlParameter("@closureSummary", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimToOrNull(closureRecord.Summary, 500)) },
                new SqlParameter("@closureOutcome", SqlDbType.NVarChar, 40) { Value = ToDbValue(TrimToOrNull(closureRecord.Outcome, 40)) },
                new SqlParameter("@closureCompletedAtUtc", SqlDbType.DateTime2) { Value = closureRecord.CompletedAtUtc.HasValue ? closureRecord.CompletedAtUtc.Value : DBNull.Value },
                new SqlParameter("@closureJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(SerializeJourneyClosure(closureRecord)) }
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

        return new AdminKanbanJourneyClosureUpdateResult
        {
            LeadId = leadId,
            JourneyId = match.JourneyId,
            CurrentState = normalizedCurrentState,
            Closure = closureRecord
        };
    }

    private void EnsureJourneyClosureSchema(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ClosureStatus') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ClosureStatus NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ClosureSummary') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ClosureSummary NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ClosureOutcome') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ClosureOutcome NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ClosureCompletedAtUtc') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ClosureCompletedAtUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}journey_executions', 'ClosureJson') IS NULL
    ALTER TABLE dbo.{TablePrefix}journey_executions ADD ClosureJson NVARCHAR(MAX) NULL;
""";
        command.ExecuteNonQuery();
    }

    private static string NormalizeJourneyClosureStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AdminKanbanJourneyClosureStatuses.Normalize(value);

    private static string NormalizeJourneyClosureOutcome(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AdminKanbanJourneyCompletionOutcomes.Normalize(value);

    private static string NormalizeJourneyReviewStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AdminKanbanJourneyReviewStatuses.Normalize(value);

    private static AdminKanbanJourneyReviewRecord SanitizeJourneyReviewRecord(AdminKanbanJourneyReviewRecord? value)
    {
        if (value is null)
        {
            return new AdminKanbanJourneyReviewRecord();
        }

        return new AdminKanbanJourneyReviewRecord
        {
            Rating = Math.Clamp(value.Rating, 0, 5),
            Comment = TrimToOrNull(value.Comment, 1000) ?? string.Empty,
            LowScoreReason = TrimToOrNull(value.LowScoreReason, 240) ?? string.Empty,
            WouldHireAgain = value.WouldHireAgain,
            SubmittedAtUtc = NormalizeJourneyUtc(value.SubmittedAtUtc)
        };
    }

    private static string? SerializeJourneyClosure(AdminKanbanJourneyClosureRecord closure) =>
        JsonSerializer.Serialize(closure);

    private static AdminKanbanJourneyClosureRecord ReadJourneyClosureRecord(SqlDataReader reader, int startIndex)
    {
        var status = reader.IsDBNull(startIndex) ? string.Empty : reader.GetString(startIndex);
        var summary = reader.IsDBNull(startIndex + 1) ? string.Empty : reader.GetString(startIndex + 1);
        var outcome = reader.IsDBNull(startIndex + 2) ? string.Empty : reader.GetString(startIndex + 2);
        var completedAtUtc = ReadNullableUtcDateTime(reader, startIndex + 3);
        var closureJson = reader.IsDBNull(startIndex + 4) ? string.Empty : reader.GetString(startIndex + 4);

        if (!string.IsNullOrWhiteSpace(closureJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<AdminKanbanJourneyClosureRecord>(closureJson);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        return new AdminKanbanJourneyClosureRecord
        {
            Status = NormalizeJourneyClosureStatus(status),
            Summary = summary,
            Outcome = NormalizeJourneyClosureOutcome(outcome),
            CompletedAtUtc = completedAtUtc
        };
    }
}
