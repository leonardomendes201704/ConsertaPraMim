namespace ConsertaPraMim.Application.DTOs;

public record AdminNoShowAuditQueryDto(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? EventType = null,
    string? Outcome = null,
    Guid? ServiceRequestId = null,
    Guid? ServiceAppointmentId = null,
    int Take = 200);

public record AdminNoShowAuditDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int Total,
    IReadOnlyList<AdminNoShowAuditItemDto> Items);

public record AdminNoShowAuditItemDto(
    Guid AuditLogId,
    Guid ServiceAppointmentId,
    Guid ServiceRequestId,
    DateTime CreatedAtUtc,
    string EventType,
    string Outcome,
    string? Source,
    string? Reason,
    decimal ServiceValue,
    string? CounterpartyActor,
    decimal CounterpartyCompensationAmount,
    decimal PenaltyAmount,
    string? LedgerResult,
    Guid ActorUserId,
    string ActorEmail);
