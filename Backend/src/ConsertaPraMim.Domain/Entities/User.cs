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
    public bool IsActive { get; set; } = true;

    // Navigation Property
    public ProviderProfile? ProviderProfile { get; set; }
    public ICollection<ServiceRequest> Requests { get; set; } = new List<ServiceRequest>();
    public ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
}
