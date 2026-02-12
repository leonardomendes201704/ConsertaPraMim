using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ProviderPlan Plan { get; set; } = ProviderPlan.Trial;
    public double RadiusKm { get; set; } = 5.0;
    public double? BaseLatitude { get; set; }
    public double? BaseLongitude { get; set; }
    public bool IsVerified { get; set; } = false;
    
    // Will need ValueConversion in EF Core
    public List<ServiceCategory> Categories { get; set; } = new(); 
    
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
}
