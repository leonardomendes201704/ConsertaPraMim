using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminChatsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminChatsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? requestId,
        Guid? providerId,
        Guid? clientId,
        string? searchTerm,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page = 1,
        int pageSize = 20)
    {
        var model = new AdminChatsIndexViewModel
        {
            Filters = NormalizeFilters(requestId, providerId, clientId, searchTerm, fromUtc, toUtc, page, pageSize)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetChatsAsync(model.Filters, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar conversas.";
            return View(model);
        }

        model.Chats = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid requestId, Guid providerId)
    {
        var model = new AdminChatDetailsViewModel();
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var chatTask = _adminOperationsApiClient.GetChatAsync(requestId, providerId, token, HttpContext.RequestAborted);
        var attachmentsTask = _adminOperationsApiClient.GetChatAttachmentsAsync(
            new AdminChatAttachmentsFilterModel
            {
                RequestId = requestId,
                Page = 1,
                PageSize = 200
            },
            token,
            HttpContext.RequestAborted);

        await Task.WhenAll(chatTask, attachmentsTask);

        if (!chatTask.Result.Success || chatTask.Result.Data == null)
        {
            if (chatTask.Result.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }

            model.ErrorMessage = chatTask.Result.ErrorMessage ?? "Falha ao carregar conversa.";
            return View(model);
        }

        model.Chat = chatTask.Result.Data;
        if (attachmentsTask.Result.Data != null)
        {
            var filteredAttachments = attachmentsTask.Result.Data.Items
                .Where(a => a.ProviderId == providerId)
                .ToList();

            model.Attachments = new ConsertaPraMim.Application.DTOs.AdminChatAttachmentsListResponseDto(
                attachmentsTask.Result.Data.Page,
                attachmentsTask.Result.Data.PageSize,
                filteredAttachments.Count,
                filteredAttachments);
        }
        return View(model);
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminChatsFilterModel NormalizeFilters(
        Guid? requestId,
        Guid? providerId,
        Guid? clientId,
        string? searchTerm,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize)
    {
        var from = fromUtc?.ToUniversalTime();
        var to = toUtc?.ToUniversalTime();
        if (from.HasValue && to.HasValue && from > to)
        {
            (from, to) = (to, from);
        }

        return new AdminChatsFilterModel
        {
            RequestId = requestId,
            ProviderId = providerId,
            ClientId = clientId,
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            FromUtc = from,
            ToUtc = to,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        };
    }
}
