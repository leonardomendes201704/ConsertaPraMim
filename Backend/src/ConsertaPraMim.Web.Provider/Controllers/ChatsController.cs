using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ChatsController : Controller
{
    private readonly IProviderBackendApiClient _backendApiClient;

    public ChatsController(IProviderBackendApiClient backendApiClient)
    {
        _backendApiClient = backendApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var (conversations, errorMessage) = await _backendApiClient.GetConversationsAsync(HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            TempData["Error"] = errorMessage;
        }

        return View(conversations);
    }
}
