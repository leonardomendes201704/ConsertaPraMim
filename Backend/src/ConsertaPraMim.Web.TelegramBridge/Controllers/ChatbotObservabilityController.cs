using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

[Authorize]
[ApiController]
[Route("api/chatbot-observability")]
public sealed class ChatbotObservabilityController : ControllerBase
{
    private readonly ITelegramChatbotObservabilityService _observabilityService;
    private readonly IOptions<TelegramChatbotObservabilityOptions> _options;
    private readonly IWebHostEnvironment _environment;

    public ChatbotObservabilityController(
        ITelegramChatbotObservabilityService observabilityService,
        IOptions<TelegramChatbotObservabilityOptions> options,
        IWebHostEnvironment environment)
    {
        _observabilityService = observabilityService;
        _options = options;
        _environment = environment;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetDashboard([FromHeader(Name = "X-Chatbot-Observability-Token")] string? dashboardToken = null)
    {
        var options = _options.Value;
        if (!options.EnableDashboardEndpoint)
        {
            return NotFound(new
            {
                errorCode = "chatbot_dashboard_disabled",
                message = "Dashboard operacional do chatbot desabilitado."
            });
        }

        if (!IsAuthorized(options, dashboardToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                errorCode = "chatbot_dashboard_forbidden",
                message = "Token de observabilidade invalido para acesso ao dashboard."
            });
        }

        return Ok(_observabilityService.GetSnapshot());
    }

    private bool IsAuthorized(TelegramChatbotObservabilityOptions options, string? requestToken)
    {
        if (_environment.IsDevelopment() && options.AllowDashboardWithoutTokenInDevelopment)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.DashboardToken))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestToken))
        {
            return false;
        }

        return requestToken.Trim().Equals(
            options.DashboardToken.Trim(),
            StringComparison.Ordinal);
    }
}
