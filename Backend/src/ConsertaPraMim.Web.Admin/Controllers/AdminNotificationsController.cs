using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminNotificationsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminNotificationsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] AdminManualNotificationWebRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Assunto e mensagem sao obrigatorios."
            });
        }

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                errorMessage = "Token administrativo ausente. Faca login novamente."
            });
        }

        Guid? recipientUserId = request.RecipientUserId;
        if (!recipientUserId.HasValue && !string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            var userByEmail = await _adminOperationsApiClient.FindUserIdByEmailAsync(
                request.RecipientEmail,
                token,
                HttpContext.RequestAborted);

            if (!userByEmail.Success || userByEmail.Data == Guid.Empty)
            {
                return NotFound(new
                {
                    success = false,
                    errorMessage = userByEmail.ErrorMessage ?? "Destinatario nao encontrado para o email informado."
                });
            }

            recipientUserId = userByEmail.Data;
        }

        if (!recipientUserId.HasValue || recipientUserId.Value == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Informe um destinatario valido."
            });
        }

        var result = await _adminOperationsApiClient.SendNotificationAsync(
            recipientUserId.Value,
            request.Subject.Trim(),
            request.Message.Trim(),
            NormalizeActionUrl(request.ActionUrl),
            string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            token,
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel enviar a notificacao.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            message = "Notificacao enviada com sucesso."
        });
    }

    private static string? NormalizeActionUrl(string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            return null;
        }

        var trimmed = actionUrl.Trim();
        return trimmed.StartsWith('/') && !trimmed.StartsWith("//")
            ? trimmed
            : null;
    }
}
