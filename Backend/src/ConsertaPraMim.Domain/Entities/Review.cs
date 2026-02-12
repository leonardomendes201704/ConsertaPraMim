using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class Review : BaseEntity
{
    public Guid RequestId { get; set; }
    public ServiceRequest Request { get; set; } = null!;
    
    public Guid ClientId { get; set; }
    public Guid ProviderId { get; set; }
    
    public int Rating { get; set; } // 1-5
    public string Comment { get; set; } = string.Empty;
}
