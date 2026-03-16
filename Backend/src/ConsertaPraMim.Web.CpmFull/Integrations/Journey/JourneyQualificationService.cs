using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyQualificationService : IJourneyQualificationService
{
    private static readonly Regex PostalCodeRegex = new(
        @"(?<!\d)\d{5}-?\d{3}(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyList<(string CategoryId, string[] Aliases)> CategoryAliases =
    [
        ("eletricista", ["eletricista", "eletrica", "chuveiro", "disjuntor", "tomada", "fiacao", "curto", "energia"]),
        ("encanador", ["encanador", "hidraulica", "vazamento", "torneira", "cano", "descarga", "esgoto"]),
        ("ar-condicionado", ["ar condicionado", "ar-condicionado", "split"]),
        ("geladeira-e-refrigeracao", ["geladeira", "freezer", "refrigeracao", "refrigerador"]),
        ("maquina-de-lavar", ["maquina de lavar", "lava e seca", "lavadora"]),
        ("fogao-e-forno", ["fogao", "forno", "cooktop"]),
        ("marcenaria", ["marcenaria", "marceneiro", "armario", "guarda roupa", "movel"]),
        ("serralheria", ["serralheria", "serralheiro", "portao", "grade", "solda"]),
        ("pintura", ["pintura", "pintor", "pintora"]),
        ("tv-e-audio", ["televisao", "tv", "audio", "som"]),
        ("celular-e-tablet", ["celular", "smartphone", "iphone", "tablet"]),
        ("computador-e-notebook", ["computador", "notebook", "pc", "impressora"]),
        ("dedetizacao", ["dedetizacao", "praga", "cupim", "barata", "formiga"]),
        ("limpeza", ["limpeza", "diarista", "faxina"])
    ];

    private readonly IMarketplaceRepository _marketplaceRepository;
    private readonly IJourneyGeocodingService _journeyGeocodingService;
    private readonly IJourneyQualificationAiGateway _journeyQualificationAiGateway;
    private readonly JourneyQualificationOptions _options;
    private readonly ILogger<JourneyQualificationService> _logger;

    public JourneyQualificationService(
        IMarketplaceRepository marketplaceRepository,
        IJourneyGeocodingService journeyGeocodingService,
        IJourneyQualificationAiGateway journeyQualificationAiGateway,
        IOptions<JourneyQualificationOptions> options,
        ILogger<JourneyQualificationService> logger)
    {
        _marketplaceRepository = marketplaceRepository;
        _journeyGeocodingService = journeyGeocodingService;
        _journeyQualificationAiGateway = journeyQualificationAiGateway;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JourneyQualificationResult> QualifyAsync(
        JourneyQualificationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var aiPayload = await TryExtractWithAiAsync(input, cancellationToken);
        var source = aiPayload is null
            ? AdminKanbanJourneyQualificationSources.Deterministic
            : AdminKanbanJourneyQualificationSources.Hybrid;

        var problemContext = ResolveProblemContext(input, aiPayload);
        var normalizedPostalCode = ResolvePostalCode(input.PostalCode, input.ProblemDescription, aiPayload);
        var normalizedCity = ResolveCity(input.City, aiPayload);
        var normalizedState = ResolveState(input.State, aiPayload);
        var normalizedStreet = ResolveStreet(input.Street, aiPayload);
        var normalizedNeighborhood = ResolveNeighborhood(input.Neighborhood, aiPayload);

        double? latitude = IsValidCoordinate(input.Latitude, -90, 90) ? input.Latitude : null;
        double? longitude = IsValidCoordinate(input.Longitude, -180, 180) ? input.Longitude : null;

        if ((!latitude.HasValue || !longitude.HasValue) && !string.IsNullOrWhiteSpace(normalizedPostalCode))
        {
            var geocoded = await _journeyGeocodingService.ResolveAsync(
                normalizedPostalCode,
                normalizedStreet,
                normalizedCity,
                cancellationToken);

            if (geocoded is not null)
            {
                normalizedPostalCode = string.IsNullOrWhiteSpace(normalizedPostalCode) ? geocoded.PostalCode : normalizedPostalCode;
                normalizedStreet = string.IsNullOrWhiteSpace(normalizedStreet) ? geocoded.Street : normalizedStreet;
                normalizedNeighborhood = string.IsNullOrWhiteSpace(normalizedNeighborhood) ? geocoded.Neighborhood : normalizedNeighborhood;
                normalizedCity = string.IsNullOrWhiteSpace(normalizedCity) ? geocoded.City : normalizedCity;
                normalizedState = string.IsNullOrWhiteSpace(normalizedState) ? geocoded.State : normalizedState;
                latitude ??= geocoded.Latitude;
                longitude ??= geocoded.Longitude;
            }
        }

        var normalizedCategory = ResolveCategory(input.ServiceCategory, problemContext, input.InternalNotes, aiPayload);
        var hasPhone = !string.IsNullOrWhiteSpace(NormalizeDigits(input.Phone));
        var hasEmail = !string.IsNullOrWhiteSpace(NormalizeEmail(input.Email));

        var requiredFields = BuildRequiredFields(input.BoardType);
        var optionalFields = BuildOptionalFields(input.BoardType);
        var missingRequiredFields = ResolveMissingRequiredFields(
            input.BoardType,
            hasPhone,
            normalizedCategory.Name,
            normalizedStreet,
            normalizedNeighborhood,
            normalizedCity,
            normalizedPostalCode,
            problemContext);

        var confidenceScore = CalculateConfidence(
            hasPhone,
            hasEmail,
            normalizedCategory.Name,
            normalizedStreet,
            normalizedNeighborhood,
            normalizedCity,
            normalizedPostalCode,
            latitude,
            longitude,
            problemContext,
            aiPayload?.ConfidenceHint);

        var hasRequiredData = missingRequiredFields.Count == 0;
        var needsConfirmation = hasRequiredData && confidenceScore < _options.MinimumConfidenceForAutoApply;
        var status = hasRequiredData
            ? needsConfirmation
                ? AdminKanbanJourneyQualificationStatuses.ConfirmationRequired
                : AdminKanbanJourneyQualificationStatuses.Qualified
            : AdminKanbanJourneyQualificationStatuses.Pending;

        return new JourneyQualificationResult
        {
            Status = status,
            Source = source,
            ConfidenceScore = confidenceScore,
            HasRequiredData = hasRequiredData,
            NeedsConfirmation = needsConfirmation,
            NormalizedServiceCategoryId = normalizedCategory.Id,
            NormalizedServiceCategoryName = normalizedCategory.Name,
            ProblemContext = TrimTo(problemContext, 1000),
            Street = TrimTo(normalizedStreet, 180),
            Neighborhood = TrimTo(normalizedNeighborhood, 120),
            City = TrimTo(normalizedCity, 120),
            State = TrimTo(normalizedState, 2),
            PostalCode = TrimTo(normalizedPostalCode, 9),
            Latitude = latitude,
            Longitude = longitude,
            Summary = BuildSummary(input.BoardType, normalizedCategory.Name, normalizedCity, normalizedPostalCode, confidenceScore, missingRequiredFields),
            ConfirmationPrompt = BuildConfirmationPrompt(input.BoardType, missingRequiredFields),
            QualifiedAtUtc = hasRequiredData ? DateTime.UtcNow : null,
            RequiredFields = requiredFields,
            MissingRequiredFields = missingRequiredFields,
            OptionalFields = optionalFields
        };
    }

    private async Task<JourneyQualificationAiPayload?> TryExtractWithAiAsync(
        JourneyQualificationInput input,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.AiEnabled || string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
        {
            return null;
        }

        try
        {
            var result = await _journeyQualificationAiGateway.ExtractAsync(
                new JourneyQualificationAiRequest
                {
                    ApiKey = _options.OpenAiApiKey,
                    Model = _options.OpenAiModel,
                    RequestTimeoutSeconds = _options.RequestTimeoutSeconds,
                    MaxRetries = _options.MaxRetries,
                    Input = input
                },
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogInformation(
                    "Qualificacao da jornada caiu para o modo deterministico. ErrorCode={ErrorCode}",
                    result.ErrorCode);
                return null;
            }

            return result.Payload;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Falha ao usar OpenAI na qualificacao da jornada; fallback deterministico sera aplicado.");
            return null;
        }
    }

    private (string Id, string Name) ResolveCategory(
        string? explicitCategory,
        string? problemContext,
        string? internalNotes,
        JourneyQualificationAiPayload? aiPayload)
    {
        var categories = _marketplaceRepository.GetCategories();
        if (categories.Count == 0)
        {
            return (string.Empty, NormalizeDisplay(explicitCategory));
        }

        var candidates = new[]
        {
            explicitCategory,
            aiPayload?.ServiceCategoryName,
            problemContext,
            internalNotes
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalizedCandidate = NormalizeToken(candidate);
            var exact = categories.FirstOrDefault(item =>
                string.Equals(NormalizeToken(item.Id), normalizedCandidate, StringComparison.Ordinal) ||
                string.Equals(NormalizeToken(item.Name), normalizedCandidate, StringComparison.Ordinal));
            if (exact is not null)
            {
                return (exact.Id, exact.Name);
            }

            var aliasMatch = CategoryAliases.FirstOrDefault(item =>
                item.Aliases.Any(alias => normalizedCandidate.Contains(NormalizeToken(alias), StringComparison.Ordinal)));
            if (!string.IsNullOrWhiteSpace(aliasMatch.CategoryId))
            {
                var categoryByAlias = categories.FirstOrDefault(item =>
                    string.Equals(NormalizeToken(item.Id), NormalizeToken(aliasMatch.CategoryId), StringComparison.Ordinal));
                if (categoryByAlias is not null)
                {
                    return (categoryByAlias.Id, categoryByAlias.Name);
                }
            }

            var fuzzy = categories.FirstOrDefault(item =>
                normalizedCandidate.Contains(NormalizeToken(item.Name), StringComparison.Ordinal) ||
                NormalizeToken(item.Name).Contains(normalizedCandidate, StringComparison.Ordinal));
            if (fuzzy is not null)
            {
                return (fuzzy.Id, fuzzy.Name);
            }
        }

        return (string.Empty, NormalizeDisplay(explicitCategory));
    }

    private static decimal CalculateConfidence(
        bool hasPhone,
        bool hasEmail,
        string? categoryName,
        string? street,
        string? neighborhood,
        string? city,
        string? postalCode,
        double? latitude,
        double? longitude,
        string? problemContext,
        decimal? aiHint)
    {
        decimal score = 0;

        if (hasPhone)
        {
            score += 0.18m;
        }

        if (hasEmail)
        {
            score += 0.05m;
        }

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            score += 0.22m;
        }

        if (!string.IsNullOrWhiteSpace(problemContext) && problemContext.Trim().Length >= 18)
        {
            score += 0.20m;
        }

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            score += 0.15m;
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            score += 0.10m;
        }

        if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(neighborhood))
        {
            score += 0.05m;
        }

        if (latitude.HasValue && longitude.HasValue)
        {
            score += 0.10m;
        }

        if (aiHint.HasValue && aiHint.Value > 0)
        {
            score = Math.Max(score, Math.Min(1m, (score + aiHint.Value) / 2m));
        }

        return Math.Round(Math.Min(score, 1m), 2, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<string> BuildRequiredFields(string boardType)
    {
        return string.Equals(AdminKanbanBoardTypes.Normalize(boardType), AdminKanbanBoardTypes.Providers, StringComparison.OrdinalIgnoreCase)
            ? ["Telefone", "Categoria tecnica", "Cidade ou regiao", "Contexto da parceria"]
            : ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"];
    }

    private static IReadOnlyList<string> BuildOptionalFields(string boardType)
    {
        return string.Equals(AdminKanbanBoardTypes.Normalize(boardType), AdminKanbanBoardTypes.Providers, StringComparison.OrdinalIgnoreCase)
            ? ["E-mail", "UF", "Latitude", "Longitude"]
            : ["E-mail", "UF", "Latitude", "Longitude"];
    }

    private static IReadOnlyList<string> ResolveMissingRequiredFields(
        string boardType,
        bool hasPhone,
        string? categoryName,
        string? street,
        string? neighborhood,
        string? city,
        string? postalCode,
        string? problemContext)
    {
        var missing = new List<string>();
        var isProvider = string.Equals(AdminKanbanBoardTypes.Normalize(boardType), AdminKanbanBoardTypes.Providers, StringComparison.OrdinalIgnoreCase);

        if (!hasPhone)
        {
            missing.Add("Telefone");
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            missing.Add(isProvider ? "Categoria tecnica" : "Categoria");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            missing.Add(isProvider ? "Cidade ou regiao" : "Cidade");
        }

        if (!isProvider && string.IsNullOrWhiteSpace(postalCode))
        {
            missing.Add("CEP");
        }

        if (!isProvider && string.IsNullOrWhiteSpace(street) && string.IsNullOrWhiteSpace(neighborhood))
        {
            missing.Add("Logradouro ou bairro");
        }

        if (string.IsNullOrWhiteSpace(problemContext) || problemContext.Trim().Length < 18)
        {
            missing.Add(isProvider ? "Contexto da parceria" : "Contexto do problema");
        }

        return missing;
    }

    private static string BuildSummary(
        string boardType,
        string? categoryName,
        string? city,
        string? postalCode,
        decimal confidenceScore,
        IReadOnlyList<string> missingRequiredFields)
    {
        if (missingRequiredFields.Count > 0)
        {
            return string.Equals(AdminKanbanBoardTypes.Normalize(boardType), AdminKanbanBoardTypes.Providers, StringComparison.OrdinalIgnoreCase)
                ? $"Qualificacao parcial do prestador. Faltam: {string.Join(", ", missingRequiredFields)}."
                : $"Qualificacao parcial do cliente. Faltam: {string.Join(", ", missingRequiredFields)}.";
        }

        var categoryFragment = string.IsNullOrWhiteSpace(categoryName) ? "categoria pendente" : $"categoria {categoryName}";
        var locationFragment = string.IsNullOrWhiteSpace(city)
            ? "localizacao pendente"
            : string.IsNullOrWhiteSpace(postalCode)
                ? $"cidade {city}"
                : $"cidade {city} / CEP {postalCode}";

        return $"Triagem estruturada concluida com {confidenceScore:P0} de confianca: {categoryFragment}, {locationFragment}.";
    }

    private static string BuildConfirmationPrompt(string boardType, IReadOnlyList<string> missingRequiredFields)
    {
        var prefix = string.Equals(AdminKanbanBoardTypes.Normalize(boardType), AdminKanbanBoardTypes.Providers, StringComparison.OrdinalIgnoreCase)
            ? "Antes de seguir com seu cadastro, confirme ou envie:"
            : "Antes de seguir com o agendamento, confirme ou envie:";

        if (missingRequiredFields.Count == 0)
        {
            return string.Equals(AdminKanbanBoardTypes.Normalize(boardType), AdminKanbanBoardTypes.Providers, StringComparison.OrdinalIgnoreCase)
                ? "Antes de seguir com seu cadastro, confirme se sua categoria tecnica, regiao e contexto de parceria estao corretos."
                : "Antes de seguir com o agendamento, confirme se categoria, endereco e contexto do problema estao corretos.";
        }

        return $"{prefix} {string.Join(", ", missingRequiredFields)}.";
    }

    private static string ResolveProblemContext(JourneyQualificationInput input, JourneyQualificationAiPayload? aiPayload)
    {
        var candidates = new[]
        {
            input.ProblemDescription,
            aiPayload?.ProblemContext,
            ExtractContextFromNotes(input.InternalNotes)
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return NormalizeWhitespace(candidate);
            }
        }

        return string.Empty;
    }

    private static string ExtractContextFromNotes(string? internalNotes)
    {
        if (string.IsNullOrWhiteSpace(internalNotes))
        {
            return string.Empty;
        }

        var lines = internalNotes
            .Split([Environment.NewLine, "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line =>
                !line.StartsWith("Origem tecnica:", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("VisitorId:", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("SessionId:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return lines.Count == 0 ? string.Empty : string.Join(" ", lines);
    }

    private static string ResolvePostalCode(string? explicitPostalCode, string? problemDescription, JourneyQualificationAiPayload? aiPayload)
    {
        var fromInput = NormalizeZip(explicitPostalCode);
        if (!string.IsNullOrWhiteSpace(fromInput))
        {
            return fromInput;
        }

        var fromAi = NormalizeZip(aiPayload?.PostalCode);
        if (!string.IsNullOrWhiteSpace(fromAi))
        {
            return fromAi;
        }

        if (!string.IsNullOrWhiteSpace(problemDescription))
        {
            var match = PostalCodeRegex.Match(problemDescription);
            if (match.Success)
            {
                return NormalizeZip(match.Value);
            }
        }

        return string.Empty;
    }

    private static string ResolveCity(string? value, JourneyQualificationAiPayload? aiPayload) =>
        NormalizeDisplay(FirstNonEmpty(value, aiPayload?.City));

    private static string ResolveState(string? value, JourneyQualificationAiPayload? aiPayload)
    {
        var state = FirstNonEmpty(value, aiPayload?.State);
        if (string.IsNullOrWhiteSpace(state))
        {
            return string.Empty;
        }

        var compact = new string(state.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        return compact.Length >= 2 ? compact[..2] : compact;
    }

    private static string ResolveStreet(string? value, JourneyQualificationAiPayload? aiPayload) =>
        NormalizeDisplay(FirstNonEmpty(value, aiPayload?.Street));

    private static string ResolveNeighborhood(string? value, JourneyQualificationAiPayload? aiPayload) =>
        NormalizeDisplay(FirstNonEmpty(value, aiPayload?.Neighborhood));

    private static bool IsValidCoordinate(double? value, double min, double max) =>
        value.HasValue &&
        !double.IsNaN(value.Value) &&
        !double.IsInfinity(value.Value) &&
        value.Value >= min &&
        value.Value <= max;

    private static string NormalizeDigits(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());

    private static string NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string NormalizeZip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 8
            ? $"{digits[..5]}-{digits[5..]}"
            : string.Empty;
    }

    private static string NormalizeDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NormalizeWhitespace(value);
        var textInfo = new CultureInfo("pt-BR").TextInfo;
        return textInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("/", " ")
            .Replace("-", " ")
            .Replace("_", " ")
            .Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string TrimTo(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
