using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Models;
using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class SupportTicketsController : Controller
{
    private readonly IProviderBackendApiClient _backendApiClient;

    public SupportTicketsController(IProviderBackendApiClient backendApiClient)
    {
        _backendApiClient = backendApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SupportTicketFiltersViewModel filters)
    {
        var normalizedFilters = NormalizeFilters(filters);
        var model = new SupportTicketsIndexViewModel
        {
            Filters = normalizedFilters
        };

        var (response, errorMessage) = await _backendApiClient.GetSupportTicketsAsync(
            normalizedFilters.Status,
            normalizedFilters.Priority,
            normalizedFilters.Search,
            normalizedFilters.Page,
            normalizedFilters.PageSize,
            HttpContext.RequestAborted);

        model.Response = response;
        model.ErrorMessage = errorMessage;

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new SupportTicketCreateViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(SupportTicketCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new MobileProviderCreateSupportTicketRequestDto(
            model.Subject,
            model.Category,
            model.Priority,
            model.InitialMessage);

        var (ticket, errorMessage) = await _backendApiClient.CreateSupportTicketAsync(request, HttpContext.RequestAborted);
        if (ticket == null)
        {
            model.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? "Nao foi possivel criar o chamado."
                : errorMessage;
            return View(model);
        }

        TempData["Success"] = "Chamado aberto com sucesso.";
        return RedirectToAction(nameof(Details), new { id = ticket.Ticket.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = new SupportTicketDetailsViewModel();
        if (id == Guid.Empty)
        {
            model.ErrorMessage = "Chamado invalido.";
            return View(model);
        }

        var (ticket, errorMessage) = await _backendApiClient.GetSupportTicketDetailsAsync(id, HttpContext.RequestAborted);
        model.Ticket = ticket;
        model.ErrorMessage = errorMessage;

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PollDetails(Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Chamado invalido."
            });
        }

        var (ticket, errorMessage) = await _backendApiClient.GetSupportTicketDetailsAsync(id, HttpContext.RequestAborted);
        if (ticket == null)
        {
            return NotFound(new
            {
                success = false,
                errorMessage = string.IsNullOrWhiteSpace(errorMessage)
                    ? "Chamado nao encontrado."
                    : errorMessage
            });
        }

        return Ok(new
        {
            success = true,
            snapshot = BuildTicketSnapshot(ticket)
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddMessage(Guid id, string message, IFormFile[]? attachments = null)
    {
        if (id == Guid.Empty)
        {
            TempData["Error"] = "Chamado invalido.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Mensagem obrigatoria.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var normalizedAttachments = new List<SupportTicketAttachmentInputDto>();
        var files = (attachments ?? Array.Empty<IFormFile>())
            .Where(file => file is { Length: > 0 })
            .ToList();
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var (uploadedAttachment, uploadError) = await _backendApiClient.UploadSupportTicketAttachmentAsync(
                id,
                stream,
                Path.GetFileName(file.FileName),
                file.ContentType,
                HttpContext.RequestAborted);

            if (uploadedAttachment == null)
            {
                TempData["Error"] = string.IsNullOrWhiteSpace(uploadError)
                    ? $"Nao foi possivel enviar o anexo '{file.FileName}'."
                    : uploadError;
                return RedirectToAction(nameof(Details), new { id });
            }

            normalizedAttachments.Add(new SupportTicketAttachmentInputDto(
                uploadedAttachment.FileUrl,
                uploadedAttachment.FileName,
                uploadedAttachment.ContentType,
                uploadedAttachment.SizeBytes));
        }

        var (ticket, errorMessage) = await _backendApiClient.AddSupportTicketMessageAsync(
            id,
            new MobileProviderSupportTicketMessageRequestDto(message, normalizedAttachments),
            HttpContext.RequestAborted);

        if (ticket == null)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(errorMessage)
                ? "Nao foi possivel enviar a mensagem."
                : errorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Mensagem enviada com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Close(Guid id)
    {
        if (id == Guid.Empty)
        {
            TempData["Error"] = "Chamado invalido.";
            return RedirectToAction(nameof(Index));
        }

        var (ticket, errorMessage) = await _backendApiClient.CloseSupportTicketAsync(id, HttpContext.RequestAborted);
        if (ticket == null)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(errorMessage)
                ? "Nao foi possivel fechar o chamado."
                : errorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Chamado fechado com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static SupportTicketFiltersViewModel NormalizeFilters(SupportTicketFiltersViewModel? filters)
    {
        var model = filters ?? new SupportTicketFiltersViewModel();
        model.Page = model.Page < 1 ? 1 : model.Page;
        model.PageSize = model.PageSize <= 0 ? 20 : Math.Min(model.PageSize, 100);
        model.Status = NormalizeOptionalText(model.Status);
        model.Priority = NormalizeOptionalText(model.Priority);
        model.Search = NormalizeOptionalText(model.Search);
        return model;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static object BuildTicketSnapshot(MobileProviderSupportTicketDetailsDto details)
    {
        var orderedMessages = (details.Messages ?? Array.Empty<MobileProviderSupportTicketMessageDto>())
            .OrderBy(message => message.CreatedAtUtc)
            .ToList();
        var lastMessage = orderedMessages.LastOrDefault();

        return new
        {
            ticketId = details.Ticket.Id,
            status = details.Ticket.Status,
            lastInteractionAtUtc = details.Ticket.LastInteractionAtUtc,
            firstAdminResponseAtUtc = details.FirstAdminResponseAtUtc,
            messageCount = orderedMessages.Count,
            lastMessageId = lastMessage?.Id,
            lastMessageCreatedAtUtc = lastMessage?.CreatedAtUtc
        };
    }
}
