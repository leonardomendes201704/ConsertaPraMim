using System.Globalization;
using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class AvailabilityController : Controller
{
    private readonly IServiceAppointmentService _serviceAppointmentService;

    public AvailabilityController(IServiceAppointmentService serviceAppointmentService)
    {
        _serviceAppointmentService = serviceAppointmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!TryGetUserId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _serviceAppointmentService.GetProviderAvailabilityOverviewAsync(
            providerId,
            "Provider",
            providerId);

        if (!result.Success || result.Overview == null)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel carregar as configuracoes de disponibilidade.";
            return View(new ProviderAvailabilityOverviewDto(providerId, Array.Empty<ProviderAvailabilityRuleDto>(), Array.Empty<ProviderAvailabilityExceptionDto>()));
        }

        return View(result.Overview);
    }

    [HttpPost]
    public async Task<IActionResult> AddRule(AddAvailabilityRuleInput input)
    {
        if (!TryGetUserId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        if (input.DayOfWeek is < 0 or > 6)
        {
            TempData["Error"] = "Dia da semana invalido.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryParseTime(input.StartTime, out var startTime) ||
            !TryParseTime(input.EndTime, out var endTime))
        {
            TempData["Error"] = "Horario invalido. Use o formato HH:mm.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _serviceAppointmentService.AddProviderAvailabilityRuleAsync(
            providerId,
            "Provider",
            new CreateProviderAvailabilityRuleRequestDto(
                providerId,
                (DayOfWeek)input.DayOfWeek,
                startTime,
                endTime,
                input.SlotDurationMinutes));

        if (result.Success)
        {
            TempData["Success"] = "Regra de disponibilidade cadastrada com sucesso.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel cadastrar a regra de disponibilidade.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveRule(Guid id)
    {
        if (!TryGetUserId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _serviceAppointmentService.RemoveProviderAvailabilityRuleAsync(providerId, "Provider", id);
        if (result.Success)
        {
            TempData["Success"] = "Regra removida com sucesso.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel remover a regra.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddBlock(AddAvailabilityBlockInput input)
    {
        if (!TryGetUserId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        if (!TryParseLocalDateTime(input.StartsAtLocal, out var startsAtLocal) ||
            !TryParseLocalDateTime(input.EndsAtLocal, out var endsAtLocal))
        {
            TempData["Error"] = "Periodo de bloqueio invalido.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _serviceAppointmentService.AddProviderAvailabilityExceptionAsync(
            providerId,
            "Provider",
            new CreateProviderAvailabilityExceptionRequestDto(
                providerId,
                startsAtLocal.ToUniversalTime(),
                endsAtLocal.ToUniversalTime(),
                input.Reason));

        if (result.Success)
        {
            TempData["Success"] = "Bloqueio cadastrado com sucesso.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel cadastrar o bloqueio.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveBlock(Guid id)
    {
        if (!TryGetUserId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _serviceAppointmentService.RemoveProviderAvailabilityExceptionAsync(providerId, "Provider", id);
        if (result.Success)
        {
            TempData["Success"] = "Bloqueio removido com sucesso.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel remover o bloqueio.";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out userId);
    }

    private static bool TryParseTime(string raw, out TimeSpan value)
    {
        return TimeSpan.TryParseExact(
            raw?.Trim(),
            new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" },
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseLocalDateTime(string raw, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                raw.Trim(),
                new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            return false;
        }

        value = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
        return true;
    }

    public sealed class AddAvailabilityRuleInput
    {
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = "08:00";
        public string EndTime { get; set; } = "18:00";
        public int SlotDurationMinutes { get; set; } = 30;
    }

    public sealed class AddAvailabilityBlockInput
    {
        public string StartsAtLocal { get; set; } = string.Empty;
        public string EndsAtLocal { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
