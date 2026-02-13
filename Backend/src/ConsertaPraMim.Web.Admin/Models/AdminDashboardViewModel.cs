namespace ConsertaPraMim.Web.Admin.Models;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalProviders { get; set; }
    public int TotalClients { get; set; }
    public int TotalAdmins { get; set; }
    public int TotalRequests { get; set; }
    public int ActiveRequests { get; set; }
}
