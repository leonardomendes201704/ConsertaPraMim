using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ApiErrorOccurrenceHourly : BaseEntity
{
    public Guid ErrorCatalogId { get; set; }
    public ApiErrorCatalog? ErrorCatalog { get; set; }
    public DateTime BucketStartUtc { get; set; }
    public string Method { get; set; } = string.Empty;
    public string EndpointTemplate { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string Severity { get; set; } = "error";
    public string TenantId { get; set; } = string.Empty;
    public long OccurrenceCount { get; set; }
}
