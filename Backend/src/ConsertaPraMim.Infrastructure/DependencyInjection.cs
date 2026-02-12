using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ConsertaPraMim.Infrastructure.Data;

namespace ConsertaPraMim.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ConsertaPraMimDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ConsertaPraMimDbContext).Assembly.FullName)));

        services.AddScoped<ConsertaPraMim.Domain.Repositories.IUserRepository, ConsertaPraMim.Infrastructure.Repositories.UserRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceRequestRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceRequestRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IProposalRepository, ConsertaPraMim.Infrastructure.Repositories.ProposalRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IReviewRepository, ConsertaPraMim.Infrastructure.Repositories.ReviewRepository>();
        
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.INotificationService, ConsertaPraMim.Infrastructure.Services.HubNotificationService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IFileStorageService, ConsertaPraMim.Infrastructure.Services.LocalFileStorageService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentService, ConsertaPraMim.Infrastructure.Services.MockPaymentService>();

        services.AddSignalR();

        return services;
    }
}
