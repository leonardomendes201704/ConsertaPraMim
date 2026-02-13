using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;

namespace ConsertaPraMim.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAuthService, ConsertaPraMim.Application.Services.AuthService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceRequestService, ConsertaPraMim.Application.Services.ServiceRequestService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProposalService, ConsertaPraMim.Application.Services.ProposalService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IReviewService, ConsertaPraMim.Application.Services.ReviewService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProfileService, ConsertaPraMim.Application.Services.ProfileService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderOnboardingService, ConsertaPraMim.Application.Services.ProviderOnboardingService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderGalleryService, ConsertaPraMim.Application.Services.ProviderGalleryService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceCategoryCatalogService, ConsertaPraMim.Application.Services.ServiceCategoryCatalogService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IChatService, ConsertaPraMim.Application.Services.ChatService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminDashboardService, ConsertaPraMim.Application.Services.AdminDashboardService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminServiceCategoryService, ConsertaPraMim.Application.Services.AdminServiceCategoryService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminUserService, ConsertaPraMim.Application.Services.AdminUserService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminRequestProposalService, ConsertaPraMim.Application.Services.AdminRequestProposalService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminChatNotificationService, ConsertaPraMim.Application.Services.AdminChatNotificationService>();
        
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
