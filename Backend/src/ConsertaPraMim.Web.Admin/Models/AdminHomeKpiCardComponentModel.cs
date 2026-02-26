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
    public string ValueCssClass { get; init; } = "metric-value";
    public int SkeletonDetailLines { get; init; } = 2;
}
