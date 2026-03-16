using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
[Route("admin/jornada")]
public sealed class JourneyInsightsController : Controller
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();
    private readonly IAdminKanbanService _kanbanService;
    private readonly JourneyGovernanceOptions _governanceOptions;

    public JourneyInsightsController(
        IAdminKanbanService kanbanService,
        IOptions<JourneyGovernanceOptions> governanceOptions)
    {
        _kanbanService = kanbanService;
        _governanceOptions = governanceOptions.Value;
    }

    [HttpGet("painel")]
    public IActionResult Index([FromQuery] AdminJourneyOperationsDashboardFilterInputModel filter)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone);
        var startDateLocal = filter.StartDate?.Date ?? nowLocal.Date.AddDays(-13);
        var endDateLocal = filter.EndDate?.Date ?? nowLocal.Date;

        if (endDateLocal < startDateLocal)
        {
            endDateLocal = startDateLocal;
        }

        var normalizedBoardType = string.IsNullOrWhiteSpace(filter.BoardType)
            ? string.Empty
            : AdminKanbanBoardTypes.Normalize(filter.BoardType);
        var normalizedSourceChannel = string.IsNullOrWhiteSpace(filter.SourceChannel)
            ? string.Empty
            : AdminKanbanJourneySourceChannels.Normalize(filter.SourceChannel);

        var snapshot = _kanbanService.GetJourneyOperationsDashboard(new AdminKanbanJourneyOperationsDashboardFilter
        {
            BoardType = string.IsNullOrWhiteSpace(normalizedBoardType) ? null : normalizedBoardType,
            SourceChannel = string.IsNullOrWhiteSpace(normalizedSourceChannel) ? null : normalizedSourceChannel,
            CreatedFromUtc = ConvertBusinessDateStartToUtc(startDateLocal),
            CreatedToUtcExclusive = ConvertBusinessDateStartToUtc(endDateLocal.AddDays(1)),
            BreakdownLimit = 8
        });

        return View(new AdminJourneyOperationsDashboardPageViewModel
        {
            Title = "Painel da jornada autonoma",
            Subtitle = "Acompanhe rollout, excecoes, conversao e gargalos da jornada automatica de servico.",
            SelectedBoardType = normalizedBoardType,
            SelectedBoardLabel = BuildBoardLabel(normalizedBoardType),
            SelectedSourceChannel = normalizedSourceChannel,
            SelectedSourceLabel = BuildSourceLabel(normalizedSourceChannel),
            PeriodStartValue = startDateLocal.ToString("yyyy-MM-dd"),
            PeriodEndValue = endDateLocal.ToString("yyyy-MM-dd"),
            PeriodLabel = $"{startDateLocal:dd/MM/yyyy} a {endDateLocal:dd/MM/yyyy} (America/Sao_Paulo)",
            Snapshot = snapshot,
            Governance = AdminJourneyGovernanceSnapshotViewModel.FromOptions(_governanceOptions)
        });
    }

    private static string BuildBoardLabel(string boardType) =>
        string.IsNullOrWhiteSpace(boardType)
            ? "Todos os boards"
            : boardType == AdminKanbanBoardTypes.Providers
                ? "Prestadores"
                : "Clientes";

    private static string BuildSourceLabel(string sourceChannel) =>
        string.IsNullOrWhiteSpace(sourceChannel)
            ? "Todos os canais"
            : AdminKanbanJourneySourceChannels.GetLabel(sourceChannel);

    private static DateTime ConvertBusinessDateStartToUtc(DateTime localDate)
    {
        var unspecified = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, BusinessTimeZone);
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
