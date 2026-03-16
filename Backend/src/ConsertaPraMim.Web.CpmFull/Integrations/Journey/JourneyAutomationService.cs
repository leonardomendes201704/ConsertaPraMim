using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyAutomationService : IJourneyAutomationService
{
    public const string SharedSecretHeaderName = "X-Journey-Automation-Key";

    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootLeadSyncService _chatwootLeadSyncService;
    private readonly IJourneyGovernanceService _journeyGovernanceService;
    private readonly IJourneyQualificationService _journeyQualificationService;
    private readonly JourneyAutomationOptions _options;
    private readonly ILogger<JourneyAutomationService> _logger;

    public JourneyAutomationService(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IJourneyGovernanceService journeyGovernanceService,
        IJourneyQualificationService journeyQualificationService,
        IOptions<JourneyAutomationOptions> options,
        ILogger<JourneyAutomationService> logger)
    {
        _kanbanService = kanbanService;
        _chatwootLeadSyncService = chatwootLeadSyncService;
        _journeyGovernanceService = journeyGovernanceService;
        _journeyQualificationService = journeyQualificationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JourneyAutomationResult> UpsertJourneyAsync(
        JourneyAutomationRequest request,
        string providedSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return JourneyAutomationResult.Fail(StatusCodes.Status409Conflict, "Automacao de jornada desabilitada no ambiente atual.");
        }

        if (!IsSecretValid(providedSecret))
        {
            return JourneyAutomationResult.Fail(StatusCodes.Status401Unauthorized, "Chave de automacao de jornada invalida.");
        }

        if (!AdminKanbanBoardTypes.IsValid(request.BoardType))
        {
            return JourneyAutomationResult.Fail(StatusCodes.Status400BadRequest, "BoardType invalido para automacao da jornada.");
        }

        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);
        if (normalizedBoardType == AdminKanbanBoardTypes.Clients && !_options.ClientsAutomationEnabled)
        {
            return JourneyAutomationResult.Fail(StatusCodes.Status409Conflict, "Automacao de jornada para clientes desabilitada no ambiente atual.");
        }

        if (normalizedBoardType == AdminKanbanBoardTypes.Providers && !_options.ProvidersAutomationEnabled)
        {
            return JourneyAutomationResult.Fail(StatusCodes.Status409Conflict, "Automacao de jornada para prestadores desabilitada no ambiente atual.");
        }

        var governanceDecision = _journeyGovernanceService.EvaluateIntake(
            normalizedBoardType,
            request.SourceChannel,
            BuildStableKey(request));
        if (!governanceDecision.Allowed)
        {
            return JourneyAutomationResult.Fail(StatusCodes.Status409Conflict, governanceDecision.Reason);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return JourneyAutomationResult.Fail(StatusCodes.Status400BadRequest, "Name e obrigatorio para automacao da jornada.");
        }

        var qualification = await _journeyQualificationService.QualifyAsync(
            new JourneyQualificationInput
            {
                BoardType = normalizedBoardType,
                SourceChannel = request.SourceChannel,
                Name = request.Name,
                Phone = request.Phone,
                Email = request.Email,
                ServiceCategory = request.ServiceCategory,
                ProblemDescription = request.ProblemDescription,
                Street = request.Street,
                Neighborhood = request.Neighborhood,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                InternalNotes = request.InternalNotes
            },
            cancellationToken);

        var upsertResult = _kanbanService.UpsertJourneyIntake(new AdminKanbanJourneyIntakeRequest
        {
            BoardType = normalizedBoardType,
            SourceChannel = request.SourceChannel,
            SourceOrigin = request.SourceOrigin,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            ServiceCategory = ResolvePreferredValue(qualification.NormalizedServiceCategoryName, request.ServiceCategory),
            ProblemDescription = ResolvePreferredValue(qualification.ProblemContext, request.ProblemDescription),
            Street = ResolvePreferredValue(qualification.Street, request.Street),
            Neighborhood = ResolvePreferredValue(qualification.Neighborhood, request.Neighborhood),
            State = ResolvePreferredValue(qualification.State, request.State),
            PostalCode = ResolvePreferredValue(qualification.PostalCode, request.PostalCode),
            City = ResolvePreferredValue(qualification.City, request.City),
            Latitude = qualification.Latitude ?? request.Latitude,
            Longitude = qualification.Longitude ?? request.Longitude,
            StatusNote = request.StatusNote,
            InternalNotes = request.InternalNotes,
            LandingLeadId = request.LandingLeadId,
            ServiceRequestId = request.ServiceRequestId,
            ClientId = request.ClientId,
            VisitorId = request.VisitorId,
            SessionId = request.SessionId,
            ChatbotConversationId = request.ChatbotConversationId,
            ChannelConversationId = request.ChannelConversationId,
            TelegramChatId = request.TelegramChatId,
            RequestedAtUtc = request.RequestedAtUtc,
            LastContactAtUtc = request.LastContactAtUtc,
            Qualification = ToQualificationRecord(qualification)
        });

        var chatwootResult = await _chatwootLeadSyncService.SyncLeadAsync(upsertResult.LeadId, cancellationToken);
        var message = upsertResult.CreatedLead
            ? "Jornada automatica criada com sucesso."
            : "Jornada automatica atualizada com sucesso.";

        _logger.LogInformation(
            "Automacao da jornada processou lead {LeadId} no board {BoardType}. JourneyId={JourneyId}. CreatedLead={CreatedLead}. CreatedJourney={CreatedJourney}. ChatwootStatus={ChatwootStatus}.",
            upsertResult.LeadId,
            upsertResult.BoardType,
            upsertResult.JourneyId,
            upsertResult.CreatedLead,
            upsertResult.CreatedJourney,
            chatwootResult.Status);

        return JourneyAutomationResult.Ok(new JourneyAutomationResponse
        {
            Success = true,
            LeadId = upsertResult.LeadId,
            JourneyId = upsertResult.JourneyId,
            JourneyPublicId = upsertResult.JourneyPublicId,
            CreatedLead = upsertResult.CreatedLead,
            CreatedJourney = upsertResult.CreatedJourney,
            BoardType = upsertResult.BoardType,
            CurrentState = upsertResult.CurrentState,
            Message = message,
            ChatwootStatus = chatwootResult.Status,
            ChatwootMessage = chatwootResult.Message,
            ChatwootContactId = chatwootResult.ContactId,
            ChatwootConversationId = chatwootResult.ConversationId,
            ChatwootInboxId = chatwootResult.InboxId
        });
    }

    private bool IsSecretValid(string providedSecret) =>
        !string.IsNullOrWhiteSpace(providedSecret) &&
        string.Equals(providedSecret.Trim(), _options.SharedSecret.Trim(), StringComparison.Ordinal);

    private static string BuildStableKey(JourneyAutomationRequest request)
    {
        if (request.TelegramChatId.HasValue && request.TelegramChatId.Value > 0)
        {
            return $"telegram:{request.TelegramChatId.Value}";
        }

        if (request.ChatbotConversationId.HasValue && request.ChatbotConversationId.Value != Guid.Empty)
        {
            return $"conversation:{request.ChatbotConversationId.Value:N}";
        }

        if (request.ServiceRequestId.HasValue && request.ServiceRequestId.Value != Guid.Empty)
        {
            return $"service-request:{request.ServiceRequestId.Value:N}";
        }

        if (request.LandingLeadId.HasValue && request.LandingLeadId.Value != Guid.Empty)
        {
            return $"landing:{request.LandingLeadId.Value:N}";
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            return $"phone:{request.Phone.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            return $"email:{request.Email.Trim().ToLowerInvariant()}";
        }

        return $"{request.SourceChannel}:{request.Name.Trim().ToLowerInvariant()}";
    }

    private static AdminKanbanJourneyQualificationRecord ToQualificationRecord(JourneyQualificationResult qualification) =>
        new()
        {
            Status = qualification.Status,
            Source = qualification.Source,
            ConfidenceScore = qualification.ConfidenceScore,
            HasRequiredData = qualification.HasRequiredData,
            NeedsConfirmation = qualification.NeedsConfirmation,
            NormalizedServiceCategoryId = qualification.NormalizedServiceCategoryId,
            NormalizedServiceCategoryName = qualification.NormalizedServiceCategoryName,
            ProblemContext = qualification.ProblemContext,
            Street = qualification.Street,
            Neighborhood = qualification.Neighborhood,
            City = qualification.City,
            State = qualification.State,
            PostalCode = qualification.PostalCode,
            Latitude = qualification.Latitude,
            Longitude = qualification.Longitude,
            Summary = qualification.Summary,
            ConfirmationPrompt = qualification.ConfirmationPrompt,
            QualifiedAtUtc = qualification.QualifiedAtUtc,
            RequiredFields = qualification.RequiredFields,
            MissingRequiredFields = qualification.MissingRequiredFields,
            OptionalFields = qualification.OptionalFields
        };

    private static string ResolvePreferredValue(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : string.IsNullOrWhiteSpace(fallback)
                ? string.Empty
                : fallback.Trim();
}
