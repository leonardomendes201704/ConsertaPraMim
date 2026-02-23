using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public ClientProfileType ClientProfileType { get; set; } = ClientProfileType.Pf;
    public ClientPjType? ClientPjType { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ProfilePictureUrl { get; set; }

    // Navigation Property
    public ProviderProfile? ProviderProfile { get; set; }
    public ICollection<ServiceRequest> Requests { get; set; } = new List<ServiceRequest>();
    public ICollection<MobilePushDevice> MobilePushDevices { get; set; } = new List<MobilePushDevice>();
    public ICollection<UserLegalTermsAcceptance> LegalTermsAcceptances { get; set; } = new List<UserLegalTermsAcceptance>();
}
