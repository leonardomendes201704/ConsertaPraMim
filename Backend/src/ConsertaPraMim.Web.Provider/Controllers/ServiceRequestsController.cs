using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Provider.Models;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ServiceRequestsController : Controller
{
    private static readonly IReadOnlyDictionary<string, int> AgendaAppointmentStatusPriority =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Confirmed"] = 1,
            ["RescheduleConfirmed"] = 2,
            ["PendingProviderConfirmation"] = 3,
            ["RescheduleRequestedByClient"] = 4,
            ["RescheduleRequestedByProvider"] = 5,
            ["Completed"] = 6,
            ["ExpiredWithoutProviderAction"] = 7,
            ["RejectedByProvider"] = 8,
            ["CancelledByClient"] = 9,
            ["CancelledByProvider"] = 10
        };

    private readonly IServiceRequestService _requestService;
    private readonly IProposalService _proposalService;
    private readonly IServiceAppointmentService _serviceAppointmentService;

    public ServiceRequestsController(
        IServiceRequestService requestService,
        IProposalService proposalService,
        IServiceAppointmentService serviceAppointmentService)
    {
        _requestService = requestService;
        _proposalService = proposalService;
        _serviceAppointmentService = serviceAppointmentService;
    }

    public async Task<IActionResult> Index(string? searchTerm)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

        var userId = Guid.Parse(userIdString);
        var matches = await _requestService.GetAllAsync(userId, "Provider", searchTerm);

        ViewBag.SearchTerm = searchTerm;
        return View(matches);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var request = await _requestService.GetByIdAsync(id, userId, "Provider");
        if (request == null) return NotFound();
        
        var myProposals = await _proposalService.GetByProviderAsync(userId);
        var existingProposal = myProposals.FirstOrDefault(p => p.RequestId == id);
        var appointment = await GetAppointmentByRequestAsync(userId, id);

        ViewBag.ExistingProposal = existingProposal;
        ViewBag.Appointment = appointment;

        return View(request);
    }

    public async Task<IActionResult> Agenda()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(
            userId,
            UserRole.Provider.ToString());
        var pendingAppointments = appointments
            .Where(a => string.Equals(a.Status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.WindowStartUtc)
            .ToList();

        var pendingConfirmationItems = new List<PendingAppointmentConfirmationViewModel>();
        foreach (var appointment in pendingAppointments)
        {
            var request = await _requestService.GetByIdAsync(
                appointment.ServiceRequestId,
                userId,
                UserRole.Provider.ToString());
            if (request == null)
            {
                continue;
            }

            pendingConfirmationItems.Add(new PendingAppointmentConfirmationViewModel(
                appointment.Id,
                appointment.ServiceRequestId,
                request.Category,
                request.Description,
                request.ClientName,
                request.Street,
                request.City,
                appointment.WindowStartUtc,
                appointment.WindowEndUtc));
        }

        ViewBag.PendingConfirmationAppointments = pendingConfirmationItems;
        ViewBag.AppointmentLookup = BuildAgendaAppointmentLookup(appointments);

        var scheduled = await _requestService.GetScheduledByProviderAsync(userId);
        return View(scheduled);
    }

    public async Task<IActionResult> History()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var history = await _requestService.GetHistoryByProviderAsync(userId);
        return View(history);
    }

    [HttpPost]
    public async Task<IActionResult> Complete(Guid id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var success = await _requestService.CompleteAsync(id, userId);
        if (success)
        {
            TempData["Success"] = "Serviço marcado como concluído com sucesso!";
        }
        else
        {
            TempData["Error"] = "Não foi possível concluir o serviço.";
        }

        return RedirectToAction("Agenda");
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmAppointment(Guid appointmentId, Guid requestId)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (appointmentId == Guid.Empty || requestId == Guid.Empty)
        {
            TempData["Error"] = "Agendamento invalido para confirmacao.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        var result = await _serviceAppointmentService.ConfirmAsync(
            userId,
            UserRole.Provider.ToString(),
            appointmentId);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel confirmar o agendamento.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        TempData["Success"] = "Visita confirmada com sucesso.";
        return RedirectToAction(nameof(Details), new { id = requestId });
    }

    [HttpPost]
    public async Task<IActionResult> RejectAppointment(Guid appointmentId, Guid requestId, string reason)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (appointmentId == Guid.Empty || requestId == Guid.Empty)
        {
            TempData["Error"] = "Agendamento invalido para recusa.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Informe o motivo da recusa do agendamento.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        var result = await _serviceAppointmentService.RejectAsync(
            userId,
            UserRole.Provider.ToString(),
            appointmentId,
            new RejectServiceAppointmentRequestDto(reason.Trim()));

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel recusar o agendamento.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        TempData["Success"] = "Agendamento recusado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = requestId });
    }

    private async Task<ServiceAppointmentDto?> GetAppointmentByRequestAsync(Guid providerId, Guid requestId)
    {
        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(
            providerId,
            UserRole.Provider.ToString());

        return appointments
            .Where(a => a.ServiceRequestId == requestId)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .FirstOrDefault();
    }

    private static IReadOnlyDictionary<Guid, ServiceAppointmentDto> BuildAgendaAppointmentLookup(
        IReadOnlyList<ServiceAppointmentDto> appointments)
    {
        var nowUtc = DateTime.UtcNow;

        return appointments
            .GroupBy(a => a.ServiceRequestId)
            .Select(group =>
            {
                var prioritized = group
                    .OrderBy(a => AgendaAppointmentStatusPriority.TryGetValue(a.Status, out var priority) ? priority : 99)
                    .ToList();

                var currentOrUpcoming = prioritized
                    .Where(a => a.WindowEndUtc >= nowUtc)
                    .OrderBy(a => a.WindowStartUtc)
                    .FirstOrDefault();

                var selected = currentOrUpcoming
                    ?? prioritized.OrderByDescending(a => a.WindowStartUtc).First();

                return new KeyValuePair<Guid, ServiceAppointmentDto>(group.Key, selected);
            })
            .ToDictionary(x => x.Key, x => x.Value);
    }
}
