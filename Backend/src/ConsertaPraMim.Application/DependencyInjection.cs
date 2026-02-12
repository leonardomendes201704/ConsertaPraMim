using Microsoft.Extensions.DependencyInjection;

namespace ConsertaPraMim.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAuthService, ConsertaPraMim.Application.Services.AuthService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceRequestService, ConsertaPraMim.Application.Services.ServiceRequestService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProposalService, ConsertaPraMim.Application.Services.ProposalService>();
        return services;
    }
}
