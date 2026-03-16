namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyQualificationInput
{
    public string BoardType { get; init; } = string.Empty;
    public string SourceChannel { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string ProblemDescription { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string InternalNotes { get; init; } = string.Empty;
}

public sealed class JourneyQualificationResult
{
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public bool HasRequiredData { get; init; }
    public bool NeedsConfirmation { get; init; }
    public string NormalizedServiceCategoryId { get; init; } = string.Empty;
    public string NormalizedServiceCategoryName { get; init; } = string.Empty;
    public string ProblemContext { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string ConfirmationPrompt { get; init; } = string.Empty;
    public DateTime? QualifiedAtUtc { get; init; }
    public IReadOnlyList<string> RequiredFields { get; init; } = [];
    public IReadOnlyList<string> MissingRequiredFields { get; init; } = [];
    public IReadOnlyList<string> OptionalFields { get; init; } = [];
}

public sealed class JourneyQualificationAiRequest
{
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; }
    public int MaxRetries { get; init; }
    public JourneyQualificationInput Input { get; init; } = new();
}

public sealed class JourneyQualificationAiResult
{
    public bool Success { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public JourneyQualificationAiPayload Payload { get; init; } = new();
}

public sealed class JourneyQualificationAiPayload
{
    public string ServiceCategoryName { get; init; } = string.Empty;
    public string ProblemContext { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public decimal ConfidenceHint { get; init; }
}

public sealed class JourneyGeocodingResult
{
    public string PostalCode { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Street { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
}
