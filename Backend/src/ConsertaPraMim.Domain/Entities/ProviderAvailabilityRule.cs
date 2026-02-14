using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderAvailabilityRule : BaseEntity
{
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public bool IsActive { get; set; } = true;
    public int SlotDurationMinutes { get; set; } = 30;
}
