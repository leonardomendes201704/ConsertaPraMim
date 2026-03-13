namespace AppMobileCPM.Models;

public sealed class ProfessionalRegistration
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Profession { get; init; }
    public required string Services { get; init; }
    public required string PostalCode { get; init; }
    public required string Phone { get; init; }
    public bool IsWhatsapp { get; init; }
    public required string Experience { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
}
