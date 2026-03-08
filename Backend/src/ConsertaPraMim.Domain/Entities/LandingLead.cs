using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class LandingLead : BaseEntity
{
    public LandingLeadOrigin Origin { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string? ServiceCategory { get; set; }
    public string? RequestedService { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyDocument { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Message { get; set; }
    public string? CurrentPageUrl { get; set; }
    public string? ReferrerUrl { get; set; }
    public string? Host { get; set; }
    public string? Scheme { get; set; }
    public string? Path { get; set; }
    public string? QueryString { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? UtmTerm { get; set; }
    public string? UtmContent { get; set; }
    public string? IpAddress { get; set; }
    public string? ForwardedFor { get; set; }
    public string? UserAgent { get; set; }
    public string? AcceptLanguage { get; set; }
    public string? BrowserLanguage { get; set; }
    public string? ScreenResolution { get; set; }
    public string? DevicePlatform { get; set; }
    public string? TimeZone { get; set; }
    public string? MetadataJson { get; set; }
}
