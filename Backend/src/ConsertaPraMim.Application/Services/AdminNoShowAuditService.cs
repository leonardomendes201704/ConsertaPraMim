using System.Globalization;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminNoShowAuditService : IAdminNoShowAuditService
{
    private const string TargetType = "ServiceAppointmentFinancialPolicy";
    private const string Action = "ServiceFinancialPolicyEventGenerated";

    private readonly IAdminAuditLogRepository _adminAuditLogRepository;

    public AdminNoShowAuditService(IAdminAuditLogRepository adminAuditLogRepository)
    {
        _adminAuditLogRepository = adminAuditLogRepository;
    }

    public async Task<AdminNoShowAuditDto> GetAuditAsync(AdminNoShowAuditQueryDto query)
    {
        var (fromUtc, toUtc) = NormalizeDateRange(query.FromUtc, query.ToUtc);
        var take = Math.Clamp(query.Take, 1, 2000);
        var logs = await _adminAuditLogRepository.GetByTargetAndPeriodAsync(
            targetType: TargetType,
            fromUtc: fromUtc,
            toUtc: toUtc,
            action: Action,
            take: take);

        var requestedEventType = NormalizeToken(query.EventType);
        var requestedOutcome = NormalizeToken(query.Outcome);

        var parsedItems = logs
            .Select(ParseItem)
            .Where(item => item != null)
            .Select(item => item!)
            .Where(item => query.ServiceAppointmentId == null || item.ServiceAppointmentId == query.ServiceAppointmentId.Value)
            .Where(item => query.ServiceRequestId == null || item.ServiceRequestId == query.ServiceRequestId.Value)
            .Where(item => requestedEventType == null || string.Equals(item.EventType, requestedEventType, StringComparison.OrdinalIgnoreCase))
            .Where(item => requestedOutcome == null || string.Equals(item.Outcome, requestedOutcome, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.AuditLogId)
            .ToList();

        return new AdminNoShowAuditDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Total: parsedItems.Count,
            Items: parsedItems);
    }

    private static AdminNoShowAuditItemDto? ParseItem(AdminAuditLog log)
    {
        if (string.IsNullOrWhiteSpace(log.Metadata))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(log.Metadata);
            var root = document.RootElement;
            var payload = TryGetProperty(root, "payload");
            if (payload == null || payload.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var eventType = ReadString(payload.Value, "eventType") ?? "Unknown";
            var outcome = ReadString(payload.Value, "outcome") ?? "unknown";
            var source = ReadString(payload.Value, "source");
            var reason = ReadString(payload.Value, "reason");
            var serviceValue = ReadDecimal(payload.Value, "serviceValue");

            var breakdown = TryGetProperty(payload.Value, "breakdown");
            var counterpartyActor = breakdown.HasValue ? ReadString(breakdown.Value, "counterpartyActorLabel") : null;
            var counterpartyCompensationAmount = breakdown.HasValue
                ? ReadDecimal(breakdown.Value, "counterpartyCompensationAmount")
                : 0m;
            var penaltyAmount = breakdown.HasValue
                ? ReadDecimal(breakdown.Value, "penaltyAmount")
                : 0m;

            var ledger = TryGetProperty(payload.Value, "ledger");
            var ledgerResult = ResolveLedgerResult(ledger);

            var appointmentId = ReadGuid(root, "appointmentId") ?? log.TargetId ?? Guid.Empty;
            var serviceRequestId = ReadGuid(root, "serviceRequestId") ?? Guid.Empty;

            return new AdminNoShowAuditItemDto(
                AuditLogId: log.Id,
                ServiceAppointmentId: appointmentId,
                ServiceRequestId: serviceRequestId,
                CreatedAtUtc: log.CreatedAt,
                EventType: eventType,
                Outcome: outcome,
                Source: source,
                Reason: reason,
                ServiceValue: serviceValue,
                CounterpartyActor: counterpartyActor,
                CounterpartyCompensationAmount: counterpartyCompensationAmount,
                PenaltyAmount: penaltyAmount,
                LedgerResult: ledgerResult,
                ActorUserId: log.ActorUserId,
                ActorEmail: log.ActorEmail);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveLedgerResult(JsonElement? ledger)
    {
        if (!ledger.HasValue || ledger.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var resultNode = TryGetProperty(ledger.Value, "result");
        if (!resultNode.HasValue || resultNode.Value.ValueKind != JsonValueKind.Object)
        {
            return "not_applied";
        }

        var success = ReadBoolean(resultNode.Value, "success");
        if (success == true)
        {
            return "applied";
        }

        var errorCode = ReadString(resultNode.Value, "errorCode");
        return string.IsNullOrWhiteSpace(errorCode)
            ? "failed"
            : $"failed:{errorCode}";
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedTo = NormalizeToUtc(toUtc ?? nowUtc);
        var normalizedFrom = NormalizeToUtc(fromUtc ?? normalizedTo.AddDays(-30));

        if (normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return (normalizedFrom, normalizedTo);
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static JsonElement? TryGetProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var property)
            ? property
            : null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        var node = TryGetProperty(element, propertyName);
        if (!node.HasValue)
        {
            return null;
        }

        return node.Value.ValueKind == JsonValueKind.String
            ? node.Value.GetString()
            : node.Value.ToString();
    }

    private static Guid? ReadGuid(JsonElement element, string propertyName)
    {
        var raw = ReadString(element, propertyName);
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static bool? ReadBoolean(JsonElement element, string propertyName)
    {
        var node = TryGetProperty(element, propertyName);
        if (!node.HasValue)
        {
            return null;
        }

        return node.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(node.Value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        var node = TryGetProperty(element, propertyName);
        if (!node.HasValue)
        {
            return 0m;
        }

        var value = node.Value;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsedDecimal))
        {
            return parsedDecimal;
        }

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out parsedDecimal))
        {
            return parsedDecimal;
        }

        return 0m;
    }
}
