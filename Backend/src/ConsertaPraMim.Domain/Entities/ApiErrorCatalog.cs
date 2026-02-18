using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ApiErrorCatalog : BaseEntity
{
    public string ErrorKey { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string NormalizedMessage { get; set; } = string.Empty;
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
