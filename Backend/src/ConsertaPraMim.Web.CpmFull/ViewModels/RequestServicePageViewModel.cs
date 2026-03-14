using AppMobileCPM.Models;

namespace AppMobileCPM.ViewModels;

public sealed class RequestServicePageViewModel
{
    public required RequestServiceInputModel Form { get; init; }
    public required IReadOnlyList<ServiceCategory> Categories { get; init; }
    public bool IsSubmitted { get; init; }
}
