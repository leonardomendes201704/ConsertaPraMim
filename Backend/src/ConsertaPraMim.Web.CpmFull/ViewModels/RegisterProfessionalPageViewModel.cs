namespace AppMobileCPM.ViewModels;

public sealed class RegisterProfessionalPageViewModel
{
    public required RegisterProfessionalInputModel Form { get; init; }
    public required IReadOnlyList<string> ProfessionOptions { get; init; }
    public bool IsSubmitted { get; init; }
}
