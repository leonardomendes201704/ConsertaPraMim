using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceRequest : BaseEntity
{
    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public ServiceCategory Category { get; set; }
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Created;
    
    public string Description { get; set; } = string.Empty;
    
    // Address
    public string AddressStreet { get; set; } = string.Empty;
    public string AddressCity { get; set; } = string.Empty;
    public string AddressZip { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public DateTime? ScheduledAt { get; set; }
    
    // Navigation
    public ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
    public Review? Review { get; set; }
}
