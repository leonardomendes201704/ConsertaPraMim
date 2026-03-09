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
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceRequestProblemAnalysisService, ConsertaPraMim.Application.Services.ServiceRequestProblemAnalysisService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceRequestCommercialValueService, ConsertaPraMim.Application.Services.ServiceRequestCommercialValueService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceAppointmentService, ConsertaPraMim.Application.Services.ServiceAppointmentService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceAppointmentNoShowRiskService, ConsertaPraMim.Application.Services.ServiceAppointmentNoShowRiskService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAppointmentReminderService, ConsertaPraMim.Application.Services.AppointmentReminderService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IGoogleCalendarSyncOperationsService, ConsertaPraMim.Application.Services.GoogleCalendarSyncOperationsService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProposalService, ConsertaPraMim.Application.Services.ProposalService>();
        services.AddScoped<ConsertaPraMim.Application.Services.ReviewService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IReviewService>(
            provider => provider.GetRequiredService<ConsertaPraMim.Application.Services.ReviewService>());
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IReviewRetentionService>(
            provider => provider.GetRequiredService<ConsertaPraMim.Application.Services.ReviewService>());
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProfileService, ConsertaPraMim.Application.Services.ProfileService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderOnboardingService, ConsertaPraMim.Application.Services.ProviderOnboardingService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderGalleryService, ConsertaPraMim.Application.Services.ProviderGalleryService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceCategoryCatalogService, ConsertaPraMim.Application.Services.ServiceCategoryCatalogService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPjRecurringContractService, ConsertaPraMim.Application.Services.PjRecurringContractService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPlanGovernanceService, ConsertaPraMim.Application.Services.PlanGovernanceService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IProviderCreditService, ConsertaPraMim.Application.Services.ProviderCreditService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminProviderCreditService, ConsertaPraMim.Application.Services.AdminProviderCreditService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IMobileClientOrderService, ConsertaPraMim.Application.Services.MobileClientOrderService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IMobileClientServiceRequestService, ConsertaPraMim.Application.Services.MobileClientServiceRequestService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IClientSupportTicketService, ConsertaPraMim.Application.Services.ClientSupportTicketService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IMobileProviderService, ConsertaPraMim.Application.Services.MobileProviderService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IMobilePushDeviceService, ConsertaPraMim.Application.Services.MobilePushDeviceService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentCheckoutService, ConsertaPraMim.Application.Services.PaymentCheckoutService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentWebhookService, ConsertaPraMim.Application.Services.PaymentWebhookService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IPaymentReceiptService, ConsertaPraMim.Application.Services.PaymentReceiptService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IChatService, ConsertaPraMim.Application.Services.ChatService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.ITelegramChatbotConversationService, ConsertaPraMim.Application.Services.TelegramChatbotConversationService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.ITelegramChatbotSchedulingService, ConsertaPraMim.Application.Services.TelegramChatbotSchedulingService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminDashboardService, ConsertaPraMim.Application.Services.AdminDashboardService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminGrowthService, ConsertaPraMim.Application.Services.AdminGrowthService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminGrowthAiService, ConsertaPraMim.Application.Services.AdminGrowthAiService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminLiquidityScoreService, ConsertaPraMim.Application.Services.AdminLiquidityScoreService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminOperationalEventNotifier, ConsertaPraMim.Application.Services.AdminOperationalEventNotifier>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowDashboardService, ConsertaPraMim.Application.Services.AdminNoShowDashboardService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowAuditService, ConsertaPraMim.Application.Services.AdminNoShowAuditService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowAlertThresholdService, ConsertaPraMim.Application.Services.AdminNoShowAlertThresholdService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowOperationalAlertService, ConsertaPraMim.Application.Services.AdminNoShowOperationalAlertService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminServiceCategoryService, ConsertaPraMim.Application.Services.AdminServiceCategoryService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminChecklistTemplateService, ConsertaPraMim.Application.Services.AdminChecklistTemplateService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminUserService, ConsertaPraMim.Application.Services.AdminUserService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminRequestProposalService, ConsertaPraMim.Application.Services.AdminRequestProposalService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminChatNotificationService, ConsertaPraMim.Application.Services.AdminChatNotificationService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminNoShowRiskPolicyService, ConsertaPraMim.Application.Services.AdminNoShowRiskPolicyService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminDisputeQueueService, ConsertaPraMim.Application.Services.AdminDisputeQueueService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminSupportTicketService, ConsertaPraMim.Application.Services.AdminSupportTicketService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminMailboxService, ConsertaPraMim.Application.Services.AdminMailboxService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminLandingLeadService, ConsertaPraMim.Application.Services.AdminLandingLeadService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminLandingAnalyticsService, ConsertaPraMim.Application.Services.AdminLandingAnalyticsService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IAdminFireTvDashboardService, ConsertaPraMim.Application.Services.AdminFireTvDashboardService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.ILandingAdminNotificationService, ConsertaPraMim.Application.Services.LandingAdminNotificationService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.ILandingAccessEventService, ConsertaPraMim.Application.Services.LandingAccessEventService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.ILandingTelemetryEventService, ConsertaPraMim.Application.Services.LandingTelemetryEventService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.ILegalTermsService, ConsertaPraMim.Application.Services.LegalTermsService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceAppointmentChecklistService, ConsertaPraMim.Application.Services.ServiceAppointmentChecklistService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.IServiceFinancialPolicyCalculationService, ConsertaPraMim.Application.Services.ServiceFinancialPolicyCalculationService>();
        services.AddScoped<ConsertaPraMim.Application.Interfaces.ILandingLeadService, ConsertaPraMim.Application.Services.LandingLeadService>();
        
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
