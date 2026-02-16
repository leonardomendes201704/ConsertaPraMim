using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceDisputeCaseRepository
{
    Task<ServiceDisputeCase?> GetByIdAsync(Guid disputeCaseId);
    Task<ServiceDisputeCase?> GetByIdWithDetailsAsync(Guid disputeCaseId);
    Task<IReadOnlyList<ServiceDisputeCase>> GetByServiceRequestIdAsync(Guid serviceRequestId);
    Task<IReadOnlyList<ServiceDisputeCase>> GetByAppointmentIdAsync(Guid appointmentId);
    Task<IReadOnlyList<ServiceDisputeCase>> GetOpenCasesAsync(int take = 200);
    Task<IReadOnlyList<ServiceDisputeCase>> GetCasesByOpenedPeriodAsync(DateTime fromUtc, DateTime toUtc, int take = 5000);
    Task<IReadOnlyList<ServiceDisputeCase>> GetClosedCasesClosedBeforeAsync(DateTime closedBeforeUtc, int take = 500);
    Task<bool> HasOpenDisputeAsync(Guid serviceRequestId);
    Task AddAsync(ServiceDisputeCase disputeCase);
    Task UpdateAsync(ServiceDisputeCase disputeCase);
    Task AddMessageAsync(ServiceDisputeCaseMessage message);
    Task UpdateMessageAsync(ServiceDisputeCaseMessage message);
    Task AddAttachmentAsync(ServiceDisputeCaseAttachment attachment);
    Task UpdateAttachmentAsync(ServiceDisputeCaseAttachment attachment);
    Task AddAuditEntryAsync(ServiceDisputeCaseAuditEntry auditEntry);
}
