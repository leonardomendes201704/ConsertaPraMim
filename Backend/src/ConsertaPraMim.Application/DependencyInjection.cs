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
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceRequestCommercialValueService, ConsertaPraMim.Application.Services.ServiceRequestCommercialValueService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceAppointmentService, ConsertaPraMim.Application.Services.ServiceAppointmentService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceAppointmentNoShowRiskService, ConsertaPraMim.Application.Services.ServiceAppointmentNoShowRiskService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAppointmentReminderService, ConsertaPraMim.Application.Services.AppointmentReminderService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProposalService, ConsertaPraMim.Application.Services.ProposalService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IReviewService, ConsertaPraMim.Application.Services.ReviewService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProfileService, ConsertaPraMim.Application.Services.ProfileService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderOnboardingService, ConsertaPraMim.Application.Services.ProviderOnboardingService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderGalleryService, ConsertaPraMim.Application.Services.ProviderGalleryService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceCategoryCatalogService, ConsertaPraMim.Application.Services.ServiceCategoryCatalogService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPlanGovernanceService, ConsertaPraMim.Application.Services.PlanGovernanceService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderCreditService, ConsertaPraMim.Application.Services.ProviderCreditService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminProviderCreditService, ConsertaPraMim.Application.Services.AdminProviderCreditService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentCheckoutService, ConsertaPraMim.Application.Services.PaymentCheckoutService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentWebhookService, ConsertaPraMim.Application.Services.PaymentWebhookService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentReceiptService, ConsertaPraMim.Application.Services.PaymentReceiptService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IChatService, ConsertaPraMim.Application.Services.ChatService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminDashboardService, ConsertaPraMim.Application.Services.AdminDashboardService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowDashboardService, ConsertaPraMim.Application.Services.AdminNoShowDashboardService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowAlertThresholdService, ConsertaPraMim.Application.Services.AdminNoShowAlertThresholdService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowOperationalAlertService, ConsertaPraMim.Application.Services.AdminNoShowOperationalAlertService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminServiceCategoryService, ConsertaPraMim.Application.Services.AdminServiceCategoryService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminChecklistTemplateService, ConsertaPraMim.Application.Services.AdminChecklistTemplateService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminUserService, ConsertaPraMim.Application.Services.AdminUserService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminRequestProposalService, ConsertaPraMim.Application.Services.AdminRequestProposalService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminChatNotificationService, ConsertaPraMim.Application.Services.AdminChatNotificationService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowRiskPolicyService, ConsertaPraMim.Application.Services.AdminNoShowRiskPolicyService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminDisputeQueueService, ConsertaPraMim.Application.Services.AdminDisputeQueueService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceAppointmentChecklistService, ConsertaPraMim.Application.Services.ServiceAppointmentChecklistService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceFinancialPolicyCalculationService, ConsertaPraMim.Application.Services.ServiceFinancialPolicyCalculationService>();
        
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
