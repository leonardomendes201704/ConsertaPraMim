namespace AppMobileCPM.Models;

public sealed class ServiceRequest
{
    public int Id { get; init; }
    public required string CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string Description { get; init; }
    public required string Location { get; init; }
    public required string Name { get; init; }
    public required string Phone { get; init; }
    public bool IsWhatsapp { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
}
