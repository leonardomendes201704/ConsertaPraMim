namespace AppMobileCPM.Models;

public sealed class Professional
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Profession { get; init; }
    public required string Description { get; init; }
    public double Rating { get; init; }
    public int Reviews { get; init; }
    public required string Distance { get; init; }
    public required IReadOnlyList<string> Services { get; init; }
    public required IReadOnlyList<string> ServicePhotoUrls { get; init; }
    public bool Verified { get; init; }
    public required string ImageUrl { get; init; }
    public required string WhatsappUrl { get; init; }
}
