using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ProposalComparisonInteraction : BaseEntity
{
    public Guid ClientUserId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? ProposalId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string SortBy { get; set; } = string.Empty;
    public string ExperimentGroup { get; set; } = "control";
    public string Source { get; set; } = "mobile_client";
}

