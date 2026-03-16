namespace AppMobileCPM.Integrations.Journey;

public static class JourneyServiceClosureTokenPurposes
{
    public const string ProviderCompletion = "provider_completion";
    public const string ClientCompletionDecision = "client_completion_decision";
    public const string ClientReview = "client_review";
    public const string ProviderReview = "provider_review";
}

public static class JourneyServiceClosureAudiences
{
    public const string Client = "cliente";
    public const string Provider = "prestador";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        Provider => Provider,
        _ => Client
    };
}

public static class JourneyServiceClosureReviewActions
{
    public const string Confirm = "confirmar";
    public const string Contest = "contestar";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        Contest => Contest,
        _ => Confirm
    };
}

public static class JourneyServiceClosureProviderOutcomes
{
    public const string Completed = "servico_concluido";
    public const string ClientNoShow = "cliente_nao_compareceu";
    public const string LateCancellation = "cancelamento_tardio";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        ClientNoShow => ClientNoShow,
        LateCancellation => LateCancellation,
        _ => Completed
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        ClientNoShow => "Cliente nao compareceu",
        LateCancellation => "Cancelamento tardio",
        _ => "Servico concluido"
    };
}

public sealed class JourneyServiceClosureSignedTokenPayload
{
    public string Purpose { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public Guid ProviderId { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}

public sealed class JourneyServiceClosureTokenValidationResult
{
    public bool Success { get; init; }
    public bool Expired { get; init; }
    public string Message { get; init; } = string.Empty;
    public JourneyServiceClosureSignedTokenPayload? Payload { get; init; }
}

public sealed class JourneyServiceClosureNotificationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class JourneyServiceClosureStartResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class JourneyServiceClosureCompletionContext
{
    public bool Success { get; init; }
    public bool TokenExpired { get; init; }
    public bool NotFound { get; init; }
    public string Message { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string RequestedCategory { get; init; } = string.Empty;
    public string AddressSummary { get; init; } = string.Empty;
    public string ScheduledWindowLabel { get; init; } = string.Empty;
    public string CompletionStatusLabel { get; init; } = string.Empty;
    public string ResponseHeadline { get; init; } = string.Empty;
    public string ResponseDescription { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
}

public sealed class JourneyServiceClosureCompletionActionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool TokenExpired { get; init; }
    public JourneyServiceClosureCompletionContext Context { get; init; } = new();
    public string NextClientReviewToken { get; init; } = string.Empty;
}

public sealed class JourneyServiceClosureReviewContext
{
    public bool Success { get; init; }
    public bool TokenExpired { get; init; }
    public bool NotFound { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string CounterpartyName { get; init; } = string.Empty;
    public string RequestedCategory { get; init; } = string.Empty;
    public string ScheduledWindowLabel { get; init; } = string.Empty;
    public string AddressSummary { get; init; } = string.Empty;
    public string ResponseHeadline { get; init; } = string.Empty;
    public string ResponseDescription { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public bool CanRespond { get; init; }
    public bool AlreadyResponded { get; init; }
}

public sealed class JourneyServiceClosureReviewSubmissionRequest
{
    public int Rating { get; init; }
    public string Comment { get; init; } = string.Empty;
    public string LowScoreReason { get; init; } = string.Empty;
    public bool? WouldHireAgain { get; init; }
}

public sealed class JourneyServiceClosureReviewActionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool TokenExpired { get; init; }
    public JourneyServiceClosureReviewContext Context { get; init; } = new();
}
