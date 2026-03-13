namespace AppMobileCPM.Services;

public interface IAdminAuthService
{
    AdminUser? ValidateCredentials(string username, string password);
    bool ChangePassword(int userId, string currentPassword, string newPassword, out string message);
    AdminDashboardStats GetDashboardStats();
}
