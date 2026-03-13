namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminSiteContentListViewModel
{
    public required IReadOnlyList<AdminSiteContentListItemViewModel> Items { get; init; }
}
