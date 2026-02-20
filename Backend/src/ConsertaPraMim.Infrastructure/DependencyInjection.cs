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
                b =>
                {
                    b.MigrationsAssembly(typeof(ConsertaPraMimDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                }));
        services.AddMemoryCache();

        services.AddScoped<ConsertaPraMim.Domain.Repositories.IUserRepository, ConsertaPraMim.Infrastructure.Repositories.UserRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IAdminAuditLogRepository, ConsertaPraMim.Infrastructure.Repositories.AdminAuditLogRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceCategoryRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceCategoryRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IProviderPlanGovernanceRepository, ConsertaPraMim.Infrastructure.Repositories.ProviderPlanGovernanceRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IProviderCreditRepository, ConsertaPraMim.Infrastructure.Repositories.ProviderCreditRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceRequestRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceRequestRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServicePaymentTransactionRepository, ConsertaPraMim.Infrastructure.Repositories.ServicePaymentTransactionRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceAppointmentRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceAppointmentRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceDisputeCaseRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceDisputeCaseRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceScopeChangeRequestRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceScopeChangeRequestRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceWarrantyClaimRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceWarrantyClaimRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IAdminNoShowDashboardRepository, ConsertaPraMim.Infrastructure.Repositories.AdminNoShowDashboardRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.INoShowAlertThresholdConfigurationRepository, ConsertaPraMim.Infrastructure.Repositories.NoShowAlertThresholdConfigurationRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceAppointmentNoShowRiskPolicyRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceAppointmentNoShowRiskPolicyRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceFinancialPolicyRuleRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceFinancialPolicyRuleRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceAppointmentNoShowQueueRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceAppointmentNoShowQueueRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceCompletionTermRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceCompletionTermRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IServiceChecklistRepository, ConsertaPraMim.Infrastructure.Repositories.ServiceChecklistRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IAppointmentReminderDispatchRepository, ConsertaPraMim.Infrastructure.Repositories.AppointmentReminderDispatchRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IAppointmentReminderPreferenceRepository, ConsertaPraMim.Infrastructure.Repositories.AppointmentReminderPreferenceRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IProposalRepository, ConsertaPraMim.Infrastructure.Repositories.ProposalRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IProviderGalleryRepository, ConsertaPraMim.Infrastructure.Repositories.ProviderGalleryRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IChatMessageRepository, ConsertaPraMim.Infrastructure.Repositories.ChatMessageRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IReviewRepository, ConsertaPraMim.Infrastructure.Repositories.ReviewRepository>();
        services.AddScoped<ConsertaPraMim.Domain.Repositories.IMobilePushDeviceRepository, ConsertaPraMim.Infrastructure.Repositories.MobilePushDeviceRepository>();
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.IMonitoringRuntimeSettings, ConsertaPraMim.Infrastructure.Services.MonitoringRuntimeSettings>();
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.ICorsRuntimeSettings, ConsertaPraMim.Infrastructure.Services.CorsRuntimeSettings>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminMonitoringService, ConsertaPraMim.Infrastructure.Services.AdminMonitoringService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminLoadTestRunService, ConsertaPraMim.Infrastructure.Services.AdminLoadTestRunService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IRequestWarningCollector, ConsertaPraMim.Infrastructure.Services.RequestWarningCollector>();
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.IRequestTelemetryBuffer, ConsertaPraMim.Infrastructure.Services.RequestTelemetryBuffer>();
        
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.INotificationService, ConsertaPraMim.Infrastructure.Services.HubNotificationService>();
        services.AddSingleton<ConsertaPraMim.Infrastructure.Services.IFirebasePushSender, ConsertaPraMim.Infrastructure.Services.FirebasePushSender>();
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.IMobilePushNotificationService, ConsertaPraMim.Infrastructure.Services.MobilePushNotificationService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IEmailService, ConsertaPraMim.Infrastructure.Services.MockEmailService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IFileStorageService, ConsertaPraMim.Infrastructure.Services.LocalFileStorageService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderGalleryMediaProcessor, ConsertaPraMim.Infrastructure.Services.ProviderGalleryMediaProcessor>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentService, ConsertaPraMim.Infrastructure.Services.MockPaymentService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IZipGeocodingService, ConsertaPraMim.Infrastructure.Services.ZipGeocodingService>();
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.IDrivingRouteService, ConsertaPraMim.Infrastructure.Services.DrivingRouteService>();
        services.AddSingleton<ConsertaPraMim.Application.Interfaces.IUserPresenceTracker, ConsertaPraMim.Infrastructure.Services.UserPresenceTracker>();
        services.AddHttpClient();

        services.AddSignalR();

        return services;
    }
}
