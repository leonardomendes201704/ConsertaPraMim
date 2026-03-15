using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
[Route("admin/telegram")]
public sealed class TelegramInsightsController : Controller
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();
    private readonly IAdminKanbanService _kanbanService;

    public TelegramInsightsController(IAdminKanbanService kanbanService)
    {
        _kanbanService = kanbanService;
    }

    [HttpGet("painel")]
    public IActionResult Index([FromQuery] AdminTelegramBusinessDashboardFilterInputModel filter)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone);
        var startDateLocal = (filter.StartDate?.Date ?? nowLocal.Date.AddDays(-6));
        var endDateLocal = (filter.EndDate?.Date ?? nowLocal.Date);

        if (endDateLocal < startDateLocal)
        {
            endDateLocal = startDateLocal;
        }

        var normalizedBoardType = string.IsNullOrWhiteSpace(filter.BoardType)
            ? string.Empty
            : AdminKanbanBoardTypes.Normalize(filter.BoardType);

        var snapshot = _kanbanService.GetTelegramBusinessDashboard(new AdminKanbanTelegramBusinessDashboardFilter
        {
            BoardType = string.IsNullOrWhiteSpace(normalizedBoardType) ? null : normalizedBoardType,
            CreatedFromUtc = ConvertBusinessDateStartToUtc(startDateLocal),
            CreatedToUtcExclusive = ConvertBusinessDateStartToUtc(endDateLocal.AddDays(1)),
            BreakdownLimit = 8
        });

        return View(new AdminTelegramBusinessDashboardPageViewModel
        {
            Title = "Painel Telegram",
            Subtitle = "Acompanhe volume, qualificacao, handoff e gargalos dos leads Telegram criados no periodo selecionado.",
            SelectedBoardType = normalizedBoardType,
            SelectedBoardLabel = BuildBoardLabel(normalizedBoardType),
            PeriodStartValue = startDateLocal.ToString("yyyy-MM-dd"),
            PeriodEndValue = endDateLocal.ToString("yyyy-MM-dd"),
            PeriodLabel = $"{startDateLocal:dd/MM/yyyy} a {endDateLocal:dd/MM/yyyy} (America/Sao_Paulo)",
            Snapshot = snapshot
        });
    }

    private static string BuildBoardLabel(string boardType) =>
        string.IsNullOrWhiteSpace(boardType)
            ? "Todos os boards"
            : boardType == AdminKanbanBoardTypes.Providers
                ? "Prestadores"
                : "Clientes";

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
