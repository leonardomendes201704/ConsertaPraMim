using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminSupportTicketsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;
    private readonly IAdminUsersApiClient _adminUsersApiClient;

    public AdminSupportTicketsController(
        IAdminOperationsApiClient adminOperationsApiClient,
        IAdminUsersApiClient adminUsersApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
        _adminUsersApiClient = adminUsersApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? status = null,
        string? priority = null,
        Guid? assignedAdminUserId = null,
        bool? assignedOnly = null,
        string? search = null,
        string? sortBy = null,
        bool sortDescending = true,
        int page = 1,
        int pageSize = 20,
        int firstResponseSlaMinutes = 60)
    {
        var viewModel = new AdminSupportTicketsIndexViewModel
        {
            Filters = NormalizeFilters(
                status,
                priority,
                assignedAdminUserId,
                assignedOnly,
                search,
                sortBy,
                sortDescending,
                page,
                pageSize,
                firstResponseSlaMinutes)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Sessao expirada. Faca login novamente.";
            return View(viewModel);
        }

        viewModel.AdminAssignees = await LoadAdminAssigneesAsync(token, HttpContext.RequestAborted);

        var result = await _adminOperationsApiClient.GetSupportTicketsAsync(
            viewModel.Filters,
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null)
        {
            viewModel.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar fila de chamados.";
            return View(viewModel);
        }

        viewModel.Tickets = result.Data;
        viewModel.LastUpdatedUtc = DateTime.UtcNow;
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var viewModel = new AdminSupportTicketDetailsPageViewModel
        {
            TicketId = id
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Sessao expirada. Faca login novamente.";
            return View(viewModel);
        }

        viewModel.AdminAssignees = await LoadAdminAssigneesAsync(token, HttpContext.RequestAborted);

        var result = await _adminOperationsApiClient.GetSupportTicketDetailsAsync(
            id,
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }

            viewModel.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar detalhes do chamado.";
            return View(viewModel);
        }

        viewModel.TicketDetails = result.Data;
        viewModel.LastUpdatedUtc = DateTime.UtcNow;
        return View(viewModel);
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

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                errorMessage = "Sessao expirada. Faca login novamente."
            });
        }

        var result = await _adminOperationsApiClient.GetSupportTicketDetailsAsync(
            id,
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null)
        {
            var statusCode = result.StatusCode.GetValueOrDefault(StatusCodes.Status502BadGateway);
            if (statusCode < 400)
            {
                statusCode = StatusCodes.Status500InternalServerError;
            }

            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Falha ao consultar atualizacoes do chamado."
            });
        }

        return Ok(new
        {
            success = true,
            snapshot = BuildTicketSnapshot(result.Data)
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddMessage(AdminSupportTicketAddMessageWebRequest request)
    {
        if (request.TicketId == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Chamado invalido.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            TempData["ErrorMessage"] = "Mensagem obrigatoria.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Sessao expirada. Faca login novamente.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        var normalizedAttachments = new List<SupportTicketAttachmentInputDto>();
        var files = (request.Attachments ?? Array.Empty<IFormFile>())
            .Where(file => file is { Length: > 0 })
            .ToList();
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var uploadResult = await _adminOperationsApiClient.UploadSupportTicketAttachmentAsync(
                request.TicketId,
                stream,
                Path.GetFileName(file.FileName),
                file.ContentType,
                token,
                HttpContext.RequestAborted);

            if (!uploadResult.Success || uploadResult.Data == null)
            {
                TempData["ErrorMessage"] = uploadResult.ErrorMessage ?? $"Nao foi possivel enviar o anexo '{file.FileName}'.";
                return RedirectToAction(nameof(Details), new { id = request.TicketId });
            }

            normalizedAttachments.Add(new SupportTicketAttachmentInputDto(
                uploadResult.Data.FileUrl,
                uploadResult.Data.FileName,
                uploadResult.Data.ContentType,
                uploadResult.Data.SizeBytes));
        }

        var response = await _adminOperationsApiClient.AddSupportTicketMessageAsync(
            request.TicketId,
            new AdminSupportTicketMessageRequestDto(
                request.Message,
                request.IsInternal,
                request.MessageType,
                request.MetadataJson,
                normalizedAttachments),
            token,
            HttpContext.RequestAborted);

        if (!response.Success)
        {
            TempData["ErrorMessage"] = response.ErrorMessage ?? "Nao foi possivel enviar a resposta.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        TempData["SuccessMessage"] = request.IsInternal
            ? "Nota interna registrada com sucesso."
            : "Resposta enviada com sucesso.";
        return RedirectToAction(nameof(Details), new { id = request.TicketId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(AdminSupportTicketStatusUpdateWebRequest request)
    {
        if (request.TicketId == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Chamado invalido.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            TempData["ErrorMessage"] = "Status obrigatorio.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Sessao expirada. Faca login novamente.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        var response = await _adminOperationsApiClient.UpdateSupportTicketStatusAsync(
            request.TicketId,
            new AdminSupportTicketStatusUpdateRequestDto(request.Status, request.Note),
            token,
            HttpContext.RequestAborted);

        if (!response.Success)
        {
            TempData["ErrorMessage"] = response.ErrorMessage ?? "Nao foi possivel atualizar o status do chamado.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        TempData["SuccessMessage"] = "Status atualizado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = request.TicketId });
    }

    [HttpPost]
    public async Task<IActionResult> Assign(AdminSupportTicketAssignWebRequest request)
    {
        if (request.TicketId == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Chamado invalido.";
            return RedirectToAction(nameof(Index));
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Sessao expirada. Faca login novamente.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        var response = await _adminOperationsApiClient.AssignSupportTicketAsync(
            request.TicketId,
            new AdminSupportTicketAssignRequestDto(request.AssignedAdminUserId, request.Note),
            token,
            HttpContext.RequestAborted);

        if (!response.Success)
        {
            TempData["ErrorMessage"] = response.ErrorMessage ?? "Nao foi possivel alterar atribuicao do chamado.";
            return RedirectToAction(nameof(Details), new { id = request.TicketId });
        }

        TempData["SuccessMessage"] = request.AssignedAdminUserId.HasValue
            ? "Chamado atribuido com sucesso."
            : "Atribuicao removida com sucesso.";
        return RedirectToAction(nameof(Details), new { id = request.TicketId });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private async Task<IReadOnlyList<AdminUserListItemDto>> LoadAdminAssigneesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var usersResult = await _adminUsersApiClient.GetUsersAsync(
            new AdminUsersFilterModel
            {
                Role = "admin",
                IsActive = true,
                Page = 1,
                PageSize = 100
            },
            accessToken,
            cancellationToken);

        if (!usersResult.Success || usersResult.Data == null)
        {
            return Array.Empty<AdminUserListItemDto>();
        }

        return usersResult.Data.Items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdminSupportTicketsFilterModel NormalizeFilters(
        string? status,
        string? priority,
        Guid? assignedAdminUserId,
        bool? assignedOnly,
        string? search,
        string? sortBy,
        bool sortDescending,
        int page,
        int pageSize,
        int firstResponseSlaMinutes)
    {
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy)
            ? "lastInteraction"
            : sortBy.Trim();

        return new AdminSupportTicketsFilterModel
        {
            Status = NormalizeOptionalFilterValue(status),
            Priority = NormalizeOptionalFilterValue(priority),
            AssignedAdminUserId = assignedAdminUserId,
            AssignedOnly = assignedOnly,
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            SortBy = normalizedSortBy,
            SortDescending = sortDescending,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            FirstResponseSlaMinutes = Math.Clamp(firstResponseSlaMinutes, 1, 10080)
        };
    }

    private static string? NormalizeOptionalFilterValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static object BuildTicketSnapshot(AdminSupportTicketDetailsDto details)
    {
        var orderedMessages = (details.Messages ?? Array.Empty<AdminSupportTicketMessageDto>())
            .OrderBy(message => message.CreatedAtUtc)
            .ToList();
        var lastMessage = orderedMessages.LastOrDefault();

        return new
        {
            ticketId = details.Ticket.Id,
            status = details.Ticket.Status,
            lastInteractionAtUtc = details.Ticket.LastInteractionAtUtc,
            firstAdminResponseAtUtc = details.Ticket.FirstAdminResponseAtUtc,
            assignedAdminUserId = details.Ticket.AssignedAdminUserId,
            messageCount = orderedMessages.Count,
            lastMessageId = lastMessage?.Id,
            lastMessageCreatedAtUtc = lastMessage?.CreatedAtUtc
        };
    }
}
