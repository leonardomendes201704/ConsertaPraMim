using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using System.Linq;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IServiceRequestRepository _requestRepository;

    public AdminController(IUserRepository userRepository, IServiceRequestRepository requestRepository)
    {
        _userRepository = userRepository;
        _requestRepository = requestRepository;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userRepository.GetAllAsync();
        var requests = await _requestRepository.GetAllAsync();

        ViewBag.TotalUsers = users.Count();
        ViewBag.TotalProviders = users.Count(u => u.Role == UserRole.Provider);
        ViewBag.TotalClients = users.Count(u => u.Role == UserRole.Client);
        ViewBag.TotalRequests = requests.Count();
        ViewBag.ActiveRequests = requests.Count(r => r.Status != ServiceRequestStatus.Completed && r.Status != ServiceRequestStatus.Canceled);

        return View(users.OrderByDescending(u => u.CreatedAt).Take(10));
    }

    public async Task<IActionResult> Users()
    {
        var users = await _userRepository.GetAllAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserStatus(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user != null)
        {
            user.IsActive = !user.IsActive;
            await _userRepository.UpdateAsync(user);
        }
        return RedirectToAction("Users");
    }
}
