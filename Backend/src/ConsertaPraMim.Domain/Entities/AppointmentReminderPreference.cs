using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class AppointmentReminderPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public AppointmentReminderChannel Channel { get; set; } = AppointmentReminderChannel.InApp;
    public bool IsEnabled { get; set; } = true;

    // Optional per-user override for reminder offsets (CSV, ex: "1440,120,30").
    public string? PreferredOffsetsMinutesCsv { get; set; }

    public DateTime? MutedUntilUtc { get; set; }
}
