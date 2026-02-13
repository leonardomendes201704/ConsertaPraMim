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
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IChatService, ConsertaPraMim.Application.Services.ChatService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminDashboardService, ConsertaPraMim.Application.Services.AdminDashboardService>();
        
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
