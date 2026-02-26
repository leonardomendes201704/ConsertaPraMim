namespace ConsertaPraMim.Web.Admin.Models;

public class AdminHomeKpiCardComponentModel
{
    public string Scope { get; init; } = "dashboard";
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string EndpointUrl { get; init; } = string.Empty;
    public string WrapperCssClass { get; init; } = string.Empty;
    public string CardCssClass { get; init; } = string.Empty;
    public string IconCssClass { get; init; } = string.Empty;
    public string TitleCssClass { get; init; } = "metric-title mb-2";
    public string ValueCssClass { get; init; } = "metric-value";
    public string CaptionCssClass { get; init; } = "metric-subtitle mt-2 d-none";
    public string DetailsContainerCssClass { get; init; } = "d-flex flex-column gap-1 mt-2";
    public string DetailCssClass { get; init; } = "metric-subtitle";
    public int SkeletonDetailLines { get; init; } = 2;
}
