using ConsertaPraMim.Web.Client.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class ChatsController : Controller
{
    private readonly IClientChatApiClient _chatApiClient;

    public ChatsController(IClientChatApiClient chatApiClient)
    {
        _chatApiClient = chatApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var (conversations, errorMessage) = await _chatApiClient.GetConversationsAsync(HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            TempData["Error"] = errorMessage;
        }

        return View(conversations);
    }
}
