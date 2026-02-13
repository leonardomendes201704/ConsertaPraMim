using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminHomeController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IServiceRequestRepository _requestRepository;

    public AdminHomeController(IUserRepository userRepository, IServiceRequestRepository requestRepository)
    {
        _userRepository = userRepository;
        _requestRepository = requestRepository;
    }

    public async Task<IActionResult> Index()
    {
        var users = (await _userRepository.GetAllAsync()).ToList();
        var requests = (await _requestRepository.GetAllAsync()).ToList();

        var viewModel = new AdminDashboardViewModel
        {
            TotalUsers = users.Count,
            TotalProviders = users.Count(u => u.Role == UserRole.Provider),
            TotalClients = users.Count(u => u.Role == UserRole.Client),
            TotalAdmins = users.Count(u => u.Role == UserRole.Admin),
            TotalRequests = requests.Count,
            ActiveRequests = requests.Count(r => r.Status != ServiceRequestStatus.Completed && r.Status != ServiceRequestStatus.Canceled)
        };

        return View(viewModel);
    }
}
