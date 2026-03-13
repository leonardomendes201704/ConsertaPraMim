namespace AppMobileCPM.Models;

public sealed class SupportRequest
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string Phone { get; init; } = string.Empty;
    public required string Category { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
}
