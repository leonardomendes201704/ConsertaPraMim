using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminMailboxController : Controller
{
    private const int ComposeRecipientsTake = 200;
    private const int MaxAttachmentCount = 10;
    private const int MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    private const int MaxTotalAttachmentsSizeBytes = 25 * 1024 * 1024;

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

        model.Recipients = await LoadComposeRecipientsAsync(token);

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

    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var model = new AdminMailboxSettingsPageViewModel
        {
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

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SaveSettings(AdminMailboxSettingsWebRequest request)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminMailboxErrorMessage"] = "Token administrativo ausente. Faca login novamente.";
            return RedirectToAction(nameof(Settings));
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

        return RedirectToAction(nameof(Settings));
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

        var recipients = await LoadComposeRecipientsAsync(token);
        var recipient = recipients.FirstOrDefault(x => x.UserId == request.RecipientUserId);
        if (recipient == null || string.IsNullOrWhiteSpace(recipient.Email))
        {
            TempData["AdminMailboxErrorMessage"] = "Destinatario invalido. Selecione um cliente ou prestador ativo.";
            return RedirectToAction(nameof(Index), new { folder = "sent" });
        }

        IReadOnlyList<AdminMailboxAttachmentDto> attachments;
        try
        {
            attachments = await MapAttachmentsAsync(request.Attachments, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminMailboxErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index), new { folder = "sent" });
        }

        var payload = new AdminMailboxSendRequestDto(
            To: recipient.Email,
            Subject: request.Subject?.Trim() ?? string.Empty,
            Body: request.Body ?? string.Empty,
            IsHtml: request.IsHtml,
            Attachments: attachments);

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

    private async Task<IReadOnlyList<AdminMailboxRecipientDto>> LoadComposeRecipientsAsync(string accessToken)
    {
        var clientsTask = _adminMailboxApiClient.GetRecipientsAsync(
            role: "client",
            search: null,
            take: ComposeRecipientsTake,
            accessToken: accessToken,
            cancellationToken: HttpContext.RequestAborted);

        var providersTask = _adminMailboxApiClient.GetRecipientsAsync(
            role: "provider",
            search: null,
            take: ComposeRecipientsTake,
            accessToken: accessToken,
            cancellationToken: HttpContext.RequestAborted);

        await Task.WhenAll(clientsTask, providersTask);

        var merged = new List<AdminMailboxRecipientDto>();
        if (clientsTask.Result.Success && clientsTask.Result.Data != null)
        {
            merged.AddRange(clientsTask.Result.Data);
        }

        if (providersTask.Result.Success && providersTask.Result.Data != null)
        {
            merged.AddRange(providersTask.Result.Data);
        }

        return merged
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.UserId)
            .Select(g => g.First())
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static async Task<IReadOnlyList<AdminMailboxAttachmentDto>> MapAttachmentsAsync(
        IEnumerable<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (files == null)
        {
            return Array.Empty<AdminMailboxAttachmentDto>();
        }

        var validFiles = files
            .Where(file => file is { Length: > 0 })
            .ToList();

        if (validFiles.Count == 0)
        {
            return Array.Empty<AdminMailboxAttachmentDto>();
        }

        if (validFiles.Count > MaxAttachmentCount)
        {
            throw new InvalidOperationException($"Quantidade de anexos excede o limite de {MaxAttachmentCount}.");
        }

        long totalBytes = 0;
        var attachments = new List<AdminMailboxAttachmentDto>(validFiles.Count);
        foreach (var file in validFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = string.IsNullOrWhiteSpace(file.FileName)
                ? "anexo.bin"
                : file.FileName.Trim();
            if (fileName.Length > 180)
            {
                fileName = fileName[..180];
            }

            if (file.Length > MaxAttachmentSizeBytes)
            {
                throw new InvalidOperationException($"Anexo '{fileName}' excede o limite de 10 MB.");
            }

            totalBytes += file.Length;
            if (totalBytes > MaxTotalAttachmentsSizeBytes)
            {
                throw new InvalidOperationException("Tamanho total dos anexos excede 25 MB.");
            }

            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            if (bytes.Length == 0)
            {
                continue;
            }

            attachments.Add(new AdminMailboxAttachmentDto(
                FileName: fileName,
                ContentType: string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType.Trim(),
                ContentBase64: Convert.ToBase64String(bytes)));
        }

        return attachments;
    }
}
