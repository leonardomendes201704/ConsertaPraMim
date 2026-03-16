using AppMobileCPM.Services;

namespace AppMobileCPM.Integrations.Journey;

public static class JourneyProviderDispatchLinkPurposes
{
    public const string OpenTracking = "open_tracking";
    public const string ResponsePage = "response_page";
}

public static class JourneyProviderOpportunityActions
{
    public const string Accept = "aceitar";
    public const string Decline = "recusar";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        Accept => Accept,
        Decline => Decline,
        _ => Accept
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        Decline => "Recusar oportunidade",
        _ => "Aceitar oportunidade"
    };

    public static string GetConfirmationLabel(string? value) => Normalize(value) switch
    {
        Decline => "Confirmar recusa",
        _ => "Confirmar aceite"
    };
}

public sealed record class JourneyProviderDispatchSignedTokenPayload
{
    public string Purpose { get; init; } = string.Empty;
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public Guid ProviderId { get; init; }
    public string TargetKey { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}

public sealed record class JourneyProviderDispatchTokenValidationResult
{
    public bool Success { get; init; }
    public bool Expired { get; init; }
    public JourneyProviderDispatchSignedTokenPayload Payload { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}

public sealed record class JourneyProviderDispatchNotificationRequest
{
    public required AdminKanbanLeadDetailsRecord Lead { get; init; }
    public required AdminKanbanJourneyDispatchTargetRecord Target { get; init; }
    public DateTime NowUtc { get; init; }
}

public sealed record class JourneyProviderDispatchNotificationResult
{
    public bool Success { get; init; }
    public bool PermanentFailure { get; init; }
    public string DeliveryChannel { get; init; } = "email";
    public string DeliveryStatus { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed record class JourneyProviderOpportunityContext
{
    public bool Success { get; init; }
    public bool TokenExpired { get; init; }
    public bool NotFound { get; init; }
    public string Message { get; init; } = string.Empty;
    public string NormalizedAction { get; init; } = JourneyProviderOpportunityActions.Accept;
    public string ResponseToken { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string RequestedCategory { get; init; } = string.Empty;
    public string RequestedSubcategory { get; init; } = string.Empty;
    public string QualificationSummary { get; init; } = string.Empty;
    public string AddressSummary { get; init; } = string.Empty;
    public string ScheduledWindowLabel { get; init; } = string.Empty;
    public string DispatchStatusLabel { get; init; } = string.Empty;
    public string TargetStatusLabel { get; init; } = string.Empty;
    public string PortalUrl { get; init; } = string.Empty;
    public bool CanRespond { get; init; }
    public bool AlreadyResponded { get; init; }
    public bool AlreadyReserved { get; init; }
    public bool ClientContactReleased { get; init; }
    public string ClientPhone { get; init; } = string.Empty;
    public string ClientEmail { get; init; } = string.Empty;
    public string ClientDisplayName { get; init; } = string.Empty;
    public string ReservedProviderPhone { get; init; } = string.Empty;
    public string ReservedProviderEmail { get; init; } = string.Empty;
    public string ResponseHeadline { get; init; } = string.Empty;
    public string ResponseDescription { get; init; } = string.Empty;
}

public sealed class JourneyProviderOpportunityActionResult
{
    public bool Success { get; init; }
    public bool TokenExpired { get; init; }
    public bool AlreadyReserved { get; init; }
    public bool AlreadyResponded { get; init; }
    public bool TargetUnavailable { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Action { get; init; } = JourneyProviderOpportunityActions.Accept;
    public JourneyProviderOpportunityContext Context { get; init; } = new();
}

public sealed record class JourneyProviderConnectionRequest
{
    public required AdminKanbanLeadDetailsRecord Lead { get; init; }
    public required AdminKanbanJourneyDispatchTargetRecord Target { get; init; }
    public DateTime ReservedAtUtc { get; init; }
}

public sealed record class JourneyProviderConnectionResult
{
    public bool Success { get; init; }
    public bool CalendarUpdated { get; init; }
    public bool ClientNotified { get; init; }
    public bool ProviderNotified { get; init; }
    public string Message { get; init; } = string.Empty;
}
