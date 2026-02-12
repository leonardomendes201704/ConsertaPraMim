using Microsoft.Extensions.DependencyInjection;

namespace ConsertaPraMim.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAuthService, ConsertaPraMim.Application.Services.AuthService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceRequestService, ConsertaPraMim.Application.Services.ServiceRequestService>();
        return services;
    }
}
