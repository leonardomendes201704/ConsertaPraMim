using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointmentNoShowRiskPolicy : BaseEntity
{
    public string Name { get; set; } = "Politica padrao de risco no-show";
    public bool IsActive { get; set; } = true;
    public int LookbackDays { get; set; } = 90;
    public int MaxHistoryEventsPerActor { get; set; } = 20;

    public int WeightClientNotConfirmed { get; set; } = 25;
    public int WeightProviderNotConfirmed { get; set; } = 25;
    public int WeightBothNotConfirmedBonus { get; set; } = 10;
    public int WeightWindowWithin24Hours { get; set; } = 10;
    public int WeightWindowWithin6Hours { get; set; } = 15;
    public int WeightWindowWithin2Hours { get; set; } = 20;
    public int WeightClientHistoryRisk { get; set; } = 10;
    public int WeightProviderHistoryRisk { get; set; } = 10;

    public int LowThresholdScore { get; set; } = 0;
    public int MediumThresholdScore { get; set; } = 40;
    public int HighThresholdScore { get; set; } = 70;

    public string? Notes { get; set; }
}
