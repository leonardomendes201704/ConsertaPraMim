using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminMailboxController : Controller
{
    private readonly IAdminMailboxApiClient _adminMailboxApiClient;

    public AdminMailboxController(IAdminMailboxApiClient adminMailboxApiClient)
    {
        _adminMailboxApiClient = adminMailboxApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string folder = "inbox",
        string? search = null,
        int page = 1,
        int pageSize = 20,
        string? selectedMessageId = null)
    {
        var filters = new AdminMailboxFilterModel
        {
            Folder = NormalizeFolder(folder),
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            SelectedMessageId = string.IsNullOrWhiteSpace(selectedMessageId) ? null : selectedMessageId.Trim()
        };

        var model = new AdminMailboxIndexViewModel
        {
            Filters = filters,
            SuccessMessage = TempData["AdminMailboxSuccessMessage"]?.ToString(),
            ErrorMessage = TempData["AdminMailboxErrorMessage"]?.ToString()
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var settingsResult = await _adminMailboxApiClient.GetSettingsAsync(token, HttpContext.RequestAborted);
        if (settingsResult.Success && settingsResult.Data != null)
        {
            model.Settings = settingsResult.Data;
        }
        else
        {
            model.ErrorMessage ??= settingsResult.ErrorMessage ?? "Falha ao carregar configuracoes do webmail.";
        }

        var listResult = await _adminMailboxApiClient.GetMessagesAsync(
            new AdminMailboxListQueryDto(filters.Folder, filters.Search, filters.Page, filters.PageSize),
            token,
            HttpContext.RequestAborted);

        if (listResult.Success && listResult.Data != null)
        {
            model.Messages = listResult.Data;
        }
        else
        {
            model.ErrorMessage ??= listResult.ErrorMessage ?? "Falha ao carregar mensagens.";
        }

        var recipientsResult = await _adminMailboxApiClient.GetRecipientsAsync(
            role: null,
            search: null,
            take: 100,
            accessToken: token,
            cancellationToken: HttpContext.RequestAborted);
        if (recipientsResult.Success && recipientsResult.Data != null)
        {
            model.Recipients = recipientsResult.Data;
        }

        var targetMessageId = filters.SelectedMessageId;
        if (string.IsNullOrWhiteSpace(targetMessageId))
        {
            targetMessageId = model.Messages.Items.FirstOrDefault()?.Id;
        }

        if (!string.IsNullOrWhiteSpace(targetMessageId))
        {
            var detailResult = await _adminMailboxApiClient.GetMessageByIdAsync(targetMessageId, token, HttpContext.RequestAborted);
            if (detailResult.Success && detailResult.Data != null)
            {
                model.SelectedMessage = detailResult.Data;
                model.Filters.SelectedMessageId = detailResult.Data.Id;
            }
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SaveSettings(AdminMailboxSettingsWebRequest request)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminMailboxErrorMessage"] = "Token administrativo ausente. Faca login novamente.";
            return RedirectToAction(nameof(Index));
        }

        var payload = new AdminMailboxUpsertSettingsRequestDto(
            Enabled: request.Enabled,
            SenderDisplayName: request.SenderDisplayName?.Trim() ?? string.Empty,
            SenderEmail: request.SenderEmail?.Trim() ?? string.Empty,
            Username: request.Username?.Trim() ?? string.Empty,
            Password: string.IsNullOrWhiteSpace(request.Password) ? null : request.Password,
            SmtpHost: request.SmtpHost?.Trim() ?? "smtp.gmail.com",
            SmtpPort: request.SmtpPort,
            SmtpUseSsl: request.SmtpUseSsl,
            Pop3Host: request.Pop3Host?.Trim() ?? "pop.gmail.com",
            Pop3Port: request.Pop3Port,
            Pop3UseSsl: request.Pop3UseSsl,
            SyncWindowSize: request.SyncWindowSize,
            PollIntervalSeconds: request.PollIntervalSeconds);

        var result = await _adminMailboxApiClient.UpsertSettingsAsync(payload, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data is { Success: false })
        {
            TempData["AdminMailboxErrorMessage"] = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel salvar configuracoes do webmail.";
        }
        else
        {
            TempData["AdminMailboxSuccessMessage"] = "Configuracoes de SMTP/POP3 salvas com sucesso.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Send(AdminMailboxSendWebRequest request)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminMailboxErrorMessage"] = "Token administrativo ausente. Faca login novamente.";
            return RedirectToAction(nameof(Index), new { folder = "sent" });
        }

        var payload = new AdminMailboxSendRequestDto(
            To: request.To?.Trim() ?? string.Empty,
            Subject: request.Subject?.Trim() ?? string.Empty,
            Body: request.Body ?? string.Empty,
            IsHtml: request.IsHtml);

        var result = await _adminMailboxApiClient.SendAsync(payload, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            TempData["AdminMailboxErrorMessage"] = result.ErrorMessage ?? "Nao foi possivel enviar o email.";
        }
        else
        {
            TempData["AdminMailboxSuccessMessage"] = "Email enviado com sucesso.";
        }

        return RedirectToAction(nameof(Index), new { folder = "sent" });
    }

    [HttpPost]
    public async Task<IActionResult> Sync(string folder = "inbox", string? search = null, int page = 1, int pageSize = 20, string? selectedMessageId = null)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminMailboxErrorMessage"] = "Token administrativo ausente. Faca login novamente.";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(folder, search, page, pageSize, selectedMessageId));
        }

        var result = await _adminMailboxApiClient.SyncAsync(token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null || !result.Data.Success)
        {
            TempData["AdminMailboxErrorMessage"] = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Falha ao sincronizar inbox.";
        }
        else
        {
            TempData["AdminMailboxSuccessMessage"] = $"Sincronizacao concluida. Novas mensagens: {result.Data.NewMessagesCount}.";
        }

        return RedirectToAction(nameof(Index), BuildIndexRouteValues(folder, search, page, pageSize, selectedMessageId));
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(AdminMailboxMarkReadWebRequest request)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminMailboxErrorMessage"] = "Token administrativo ausente. Faca login novamente.";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(request.Folder, request.Search, request.Page, request.PageSize, request.MessageId));
        }

        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            TempData["AdminMailboxErrorMessage"] = "Mensagem invalida para atualizacao de leitura.";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(request.Folder, request.Search, request.Page, request.PageSize, null));
        }

        var result = await _adminMailboxApiClient.MarkMessageReadAsync(request.MessageId, request.IsRead, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            TempData["AdminMailboxErrorMessage"] = result.ErrorMessage ?? "Falha ao atualizar status de leitura.";
        }
        else
        {
            TempData["AdminMailboxSuccessMessage"] = request.IsRead
                ? "Mensagem marcada como lida."
                : "Mensagem marcada como nao lida.";
        }

        return RedirectToAction(
            nameof(Index),
            BuildIndexRouteValues(request.Folder, request.Search, request.Page, request.PageSize, request.MessageId));
    }

    private static object BuildIndexRouteValues(string folder, string? search, int page, int pageSize, string? selectedMessageId)
    {
        return new
        {
            folder = NormalizeFolder(folder),
            search,
            page = Math.Max(1, page),
            pageSize = Math.Clamp(pageSize, 1, 100),
            selectedMessageId
        };
    }

    private static string NormalizeFolder(string? folder)
    {
        var normalized = (folder ?? "inbox").Trim().ToLowerInvariant();
        return normalized is "inbox" or "sent" ? normalized : "inbox";
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }
}
