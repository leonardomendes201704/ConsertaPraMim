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
    private const long ChecklistEvidenceMaxFileSizeBytes = 25_000_000;
    private static readonly HashSet<string> ChecklistAllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private static readonly HashSet<string> ChecklistAllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "video/mp4", "video/webm", "video/quicktime"
    };

    private static readonly IReadOnlyDictionary<string, int> AgendaAppointmentStatusPriority =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["InProgress"] = 1,
            ["Arrived"] = 2,
            ["Confirmed"] = 3,
            ["RescheduleConfirmed"] = 4,
            ["PendingProviderConfirmation"] = 5,
            ["RescheduleRequestedByClient"] = 6,
            ["RescheduleRequestedByProvider"] = 7,
            ["Completed"] = 8,
            ["ExpiredWithoutProviderAction"] = 9,
            ["RejectedByProvider"] = 10,
            ["CancelledByClient"] = 11,
            ["CancelledByProvider"] = 12
        };

    private readonly IServiceRequestService _requestService;
    private readonly IProposalService _proposalService;
    private readonly IServiceAppointmentService _serviceAppointmentService;
    private readonly IServiceAppointmentChecklistService _serviceAppointmentChecklistService;
    private readonly IFileStorageService _fileStorageService;

    public ServiceRequestsController(
        IServiceRequestService requestService,
        IProposalService proposalService,
        IServiceAppointmentService serviceAppointmentService,
        IServiceAppointmentChecklistService serviceAppointmentChecklistService,
        IFileStorageService fileStorageService)
    {
        _requestService = requestService;
        _proposalService = proposalService;
        _serviceAppointmentService = serviceAppointmentService;
        _serviceAppointmentChecklistService = serviceAppointmentChecklistService;
        _fileStorageService = fileStorageService;
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
        var scopeChanges = await _serviceAppointmentService.GetScopeChangeRequestsByServiceRequestAsync(
            userId,
            UserRole.Provider.ToString(),
            id);
        ServiceAppointmentChecklistDto? checklist = null;
        ServiceCompletionTermDto? completionTerm = null;
        if (appointment != null)
        {
            var checklistResult = await _serviceAppointmentChecklistService.GetChecklistAsync(
                userId,
                UserRole.Provider.ToString(),
                appointment.Id);
            if (checklistResult.Success)
            {
                checklist = checklistResult.Checklist;
            }

            var completionResult = await _serviceAppointmentService.GetCompletionTermAsync(
                userId,
                UserRole.Provider.ToString(),
                appointment.Id);
            if (completionResult.Success)
            {
                completionTerm = completionResult.Term;
            }
        }

        ViewBag.ExistingProposal = existingProposal;
        ViewBag.Appointment = appointment;
        ViewBag.ScopeChanges = scopeChanges;
        ViewBag.AppointmentChecklist = checklist;
        ViewBag.CompletionTerm = completionTerm;

        return View(request);
    }

    [HttpGet]
    public async Task<IActionResult> PaymentReceiptsData(
        Guid id,
        [FromServices] IPaymentReceiptService paymentReceiptService)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var receipts = await paymentReceiptService.GetByServiceRequestAsync(
            userId,
            UserRole.Provider.ToString(),
            id);

        return Json(new
        {
            receipts = receipts.Select(r => new
            {
                transactionId = r.TransactionId,
                serviceRequestId = r.ServiceRequestId,
                clientId = r.ClientId,
                clientName = r.ClientName,
                providerId = r.ProviderId,
                providerName = r.ProviderName,
                amount = r.Amount,
                currency = r.Currency,
                method = r.Method,
                status = r.Status,
                createdAtUtc = r.CreatedAtUtc,
                processedAtUtc = r.ProcessedAtUtc,
                refundedAtUtc = r.RefundedAtUtc,
                expiresAtUtc = r.ExpiresAtUtc,
                providerTransactionId = r.ProviderTransactionId,
                checkoutReference = r.CheckoutReference,
                receiptNumber = r.ReceiptNumber,
                receiptUrl = r.ReceiptUrl
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentReceipt(
        Guid requestId,
        Guid transactionId,
        [FromServices] IPaymentReceiptService paymentReceiptService)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var result = await paymentReceiptService.GetByTransactionAsync(
            userId,
            UserRole.Provider.ToString(),
            requestId,
            transactionId);

        if (!result.Success || result.Receipt == null)
        {
            return result.ErrorCode switch
            {
                "forbidden" => Forbid(),
                "request_not_found" => NotFound(),
                "transaction_not_found" => NotFound(),
                _ => BadRequest(new { message = result.ErrorMessage ?? "Nao foi possivel gerar o comprovante." })
            };
        }

        return View("PaymentReceipt", result.Receipt);
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
                appointment.WindowEndUtc,
                appointment.NoShowRiskScore,
                appointment.NoShowRiskLevel,
                appointment.NoShowRiskReasons,
                appointment.NoShowRiskCalculatedAtUtc));
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

        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(
            userId,
            UserRole.Provider.ToString());

        var appointment = appointments
            .Where(a => a.ServiceRequestId == id)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .FirstOrDefault();

        if (appointment == null)
        {
            TempData["Error"] = "Nao foi encontrado agendamento para concluir este servico.";
            return RedirectToAction("Agenda");
        }

        var result = await _serviceAppointmentService.UpdateOperationalStatusAsync(
            userId,
            UserRole.Provider.ToString(),
            appointment.Id,
            new UpdateServiceAppointmentOperationalStatusRequestDto(
                ServiceAppointmentOperationalStatus.Completed.ToString(),
                "Finalizacao pelo portal do prestador."));

        if (result.Success)
        {
            TempData["Success"] = "Servico marcado como concluido com sucesso.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel concluir o servico.";
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

    [HttpPost]
    public async Task<IActionResult> RespondAppointmentPresence(
        Guid appointmentId,
        Guid requestId,
        bool confirmed,
        string? reason,
        bool returnToAgenda = false)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (appointmentId == Guid.Empty || requestId == Guid.Empty)
        {
            TempData["Error"] = "Agendamento invalido para responder presenca.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        var result = await _serviceAppointmentService.RespondPresenceAsync(
            userId,
            UserRole.Provider.ToString(),
            appointmentId,
            new RespondServiceAppointmentPresenceRequestDto(
                confirmed,
                string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel registrar sua resposta de presenca.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        TempData["Success"] = confirmed
            ? "Presenca confirmada com sucesso."
            : "Resposta de nao confirmacao registrada.";
        return returnToAgenda
            ? RedirectToAction(nameof(Agenda))
            : RedirectToAction(nameof(Details), new { id = requestId });
    }

    [HttpPost]
    public async Task<IActionResult> MarkArrival(
        Guid appointmentId,
        Guid requestId,
        double? latitude,
        double? longitude,
        double? accuracyMeters,
        string? manualReason,
        bool returnToAgenda = false)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (appointmentId == Guid.Empty || requestId == Guid.Empty)
        {
            TempData["Error"] = "Agendamento invalido para registrar chegada.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        var result = await _serviceAppointmentService.MarkArrivedAsync(
            userId,
            UserRole.Provider.ToString(),
            appointmentId,
            new MarkServiceAppointmentArrivalRequestDto(latitude, longitude, accuracyMeters, manualReason));

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel registrar chegada.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        TempData["Success"] = "Chegada registrada com sucesso.";
        return returnToAgenda
            ? RedirectToAction(nameof(Agenda))
            : RedirectToAction(nameof(Details), new { id = requestId });
    }

    [HttpPost]
    public async Task<IActionResult> StartAppointment(
        Guid appointmentId,
        Guid requestId,
        string? reason,
        bool returnToAgenda = false)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (appointmentId == Guid.Empty || requestId == Guid.Empty)
        {
            TempData["Error"] = "Agendamento invalido para iniciar atendimento.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        var result = await _serviceAppointmentService.StartExecutionAsync(
            userId,
            UserRole.Provider.ToString(),
            appointmentId,
            new StartServiceAppointmentExecutionRequestDto(reason));

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel iniciar atendimento.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        TempData["Success"] = "Atendimento iniciado com sucesso.";
        return returnToAgenda
            ? RedirectToAction(nameof(Agenda))
            : RedirectToAction(nameof(Details), new { id = requestId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateAppointmentOperationalStatus(
        Guid appointmentId,
        Guid requestId,
        string operationalStatus,
        string? reason,
        bool returnToAgenda = false)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (appointmentId == Guid.Empty || requestId == Guid.Empty)
        {
            TempData["Error"] = "Agendamento invalido para atualizar status operacional.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        var result = await _serviceAppointmentService.UpdateOperationalStatusAsync(
            userId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto(operationalStatus, reason));

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel atualizar o status operacional.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        TempData["Success"] = "Status operacional atualizado com sucesso.";
        return returnToAgenda
            ? RedirectToAction(nameof(Agenda))
            : RedirectToAction(nameof(Details), new { id = requestId });
    }

    [HttpPost]
    [RequestSizeLimit(120_000_000)]
    public async Task<IActionResult> UpdateChecklistItem(
        Guid appointmentId,
        Guid requestId,
        Guid templateItemId,
        bool isChecked,
        string? note,
        bool clearEvidence = false,
        IFormFile? evidenceFile = null,
        bool returnToAgenda = false)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (appointmentId == Guid.Empty || requestId == Guid.Empty || templateItemId == Guid.Empty)
        {
            TempData["Error"] = "Dados invalidos para atualizar checklist.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        string? evidenceUrl = null;
        string? evidenceFileName = null;
        string? evidenceContentType = null;
        long? evidenceSizeBytes = null;

        if (evidenceFile is { Length: > 0 })
        {
            var validationError = ValidateChecklistEvidenceFile(evidenceFile);
            if (validationError != null)
            {
                TempData["Error"] = validationError;
                return returnToAgenda
                    ? RedirectToAction(nameof(Agenda))
                    : RedirectToAction(nameof(Details), new { id = requestId });
            }

            await using var stream = evidenceFile.OpenReadStream();
            if (!IsSupportedChecklistEvidenceSignature(stream, evidenceFile.ContentType))
            {
                TempData["Error"] = "Arquivo de evidencia com assinatura invalida.";
                return returnToAgenda
                    ? RedirectToAction(nameof(Agenda))
                    : RedirectToAction(nameof(Details), new { id = requestId });
            }

            stream.Position = 0;
            evidenceUrl = await _fileStorageService.SaveFileAsync(stream, evidenceFile.FileName, "service-checklists");
            evidenceFileName = Path.GetFileName(evidenceFile.FileName);
            evidenceContentType = evidenceFile.ContentType;
            evidenceSizeBytes = evidenceFile.Length;
        }

        var result = await _serviceAppointmentChecklistService.UpsertItemResponseAsync(
            userId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpsertServiceChecklistItemResponseRequestDto(
                templateItemId,
                isChecked,
                note,
                evidenceUrl,
                evidenceFileName,
                evidenceContentType,
                evidenceSizeBytes,
                clearEvidence));

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel atualizar item do checklist.";
            return returnToAgenda
                ? RedirectToAction(nameof(Agenda))
                : RedirectToAction(nameof(Details), new { id = requestId });
        }

        TempData["Success"] = "Checklist atualizado com sucesso.";
        return returnToAgenda
            ? RedirectToAction(nameof(Agenda))
            : RedirectToAction(nameof(Details), new { id = requestId });
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

    private static string? ValidateChecklistEvidenceFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return "Selecione um arquivo de evidencia.";
        }

        if (file.Length > ChecklistEvidenceMaxFileSizeBytes)
        {
            return "Arquivo de evidencia acima de 25MB.";
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !ChecklistAllowedExtensions.Contains(extension))
        {
            return "Extensao de evidencia nao permitida.";
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !ChecklistAllowedContentTypes.Contains(file.ContentType))
        {
            return "Tipo de conteudo da evidencia nao permitido.";
        }

        return null;
    }

    private static bool IsSupportedChecklistEvidenceSignature(Stream stream, string contentType)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            return false;
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return IsSupportedImageSignature(stream);
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return IsSupportedVideoSignature(stream);
        }

        return false;
    }

    private static bool IsSupportedImageSignature(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[12];
        var bytesRead = stream.Read(buffer);
        if (bytesRead < 12)
        {
            return false;
        }

        var isJpeg = buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF;
        var isPng = buffer[0] == 0x89 &&
                    buffer[1] == 0x50 &&
                    buffer[2] == 0x4E &&
                    buffer[3] == 0x47 &&
                    buffer[4] == 0x0D &&
                    buffer[5] == 0x0A &&
                    buffer[6] == 0x1A &&
                    buffer[7] == 0x0A;
        var isWebp = buffer[0] == 0x52 &&
                     buffer[1] == 0x49 &&
                     buffer[2] == 0x46 &&
                     buffer[3] == 0x46 &&
                     buffer[8] == 0x57 &&
                     buffer[9] == 0x45 &&
                     buffer[10] == 0x42 &&
                     buffer[11] == 0x50;

        return isJpeg || isPng || isWebp;
    }

    private static bool IsSupportedVideoSignature(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[16];
        var bytesRead = stream.Read(buffer);
        if (bytesRead < 12)
        {
            return false;
        }

        var isMp4OrMov = buffer[4] == 0x66 &&
                         buffer[5] == 0x74 &&
                         buffer[6] == 0x79 &&
                         buffer[7] == 0x70;

        var isWebm = buffer[0] == 0x1A &&
                     buffer[1] == 0x45 &&
                     buffer[2] == 0xDF &&
                     buffer[3] == 0xA3;

        return isMp4OrMov || isWebm;
    }
}

