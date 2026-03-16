using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramLeadAutomationService : ITelegramLeadAutomationService
{
    public const string SharedSecretHeaderName = "X-Telegram-Automation-Key";

    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootLeadSyncService _chatwootLeadSyncService;
    private readonly IJourneyQualificationService _journeyQualificationService;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramLeadAutomationService> _logger;

    public TelegramLeadAutomationService(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IJourneyQualificationService journeyQualificationService,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramLeadAutomationService> logger)
    {
        _kanbanService = kanbanService;
        _chatwootLeadSyncService = chatwootLeadSyncService;
        _journeyQualificationService = journeyQualificationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TelegramLeadAutomationResult> UpsertLeadAsync(
        TelegramLeadAutomationRequest request,
        string providedSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status409Conflict,
                "Automacao Telegram desabilitada no ambiente atual.");
        }

        if (!IsSecretValid(providedSecret))
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status401Unauthorized,
                "Chave de automacao Telegram invalida.");
        }

        if (!AdminKanbanBoardTypes.IsValid(request.BoardType))
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status400BadRequest,
                "BoardType invalido para automacao Telegram.");
        }

        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);
        if (normalizedBoardType == AdminKanbanBoardTypes.Clients && !_options.ClientsAutomationEnabled)
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status409Conflict,
                "Automacao Telegram para clientes desabilitada no ambiente atual.");
        }

        if (normalizedBoardType == AdminKanbanBoardTypes.Providers && !_options.ProvidersAutomationEnabled)
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status409Conflict,
                "Automacao Telegram para prestadores desabilitada no ambiente atual.");
        }

        if (request.ChatbotConversationId == Guid.Empty)
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status400BadRequest,
                "ChatbotConversationId e obrigatorio para automacao Telegram.");
        }

        if (request.TelegramChatId <= 0)
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status400BadRequest,
                "TelegramChatId deve ser maior que zero para automacao Telegram.");
        }

        if (request.UserId == Guid.Empty)
        {
            return TelegramLeadAutomationResult.Fail(
                StatusCodes.Status400BadRequest,
                "UserId e obrigatorio para automacao Telegram.");
        }

        var qualification = await _journeyQualificationService.QualifyAsync(
            new JourneyQualificationInput
            {
                BoardType = normalizedBoardType,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                Name = string.IsNullOrWhiteSpace(request.UserName)
                    ? (normalizedBoardType == AdminKanbanBoardTypes.Providers ? "Prestador Telegram" : "Cliente Telegram")
                    : request.UserName.Trim(),
                Phone = request.UserPhone,
                Email = request.UserEmail,
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
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SourceOrigin = string.IsNullOrWhiteSpace(request.ChannelConversationId) ? "telegram-bot" : request.ChannelConversationId,
            Name = string.IsNullOrWhiteSpace(request.UserName)
                ? (normalizedBoardType == AdminKanbanBoardTypes.Providers ? "Prestador Telegram" : "Cliente Telegram")
                : request.UserName.Trim(),
            Phone = request.UserPhone,
            Email = request.UserEmail,
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
            ServiceRequestId = request.ServiceRequestId,
            ClientId = request.UserId,
            ChatbotConversationId = request.ChatbotConversationId,
            ChannelConversationId = request.ChannelConversationId,
            TelegramChatId = request.TelegramChatId,
            RequestedAtUtc = request.LastContactAtUtc,
            LastContactAtUtc = request.LastContactAtUtc,
            Qualification = ToQualificationRecord(qualification)
        });

        var chatwootResult = await _chatwootLeadSyncService.SyncLeadAsync(upsertResult.LeadId, cancellationToken);
        var leadDetails = _kanbanService.GetLeadDetails(upsertResult.LeadId);
        var journeyDetails = _kanbanService.GetJourneyDetails(upsertResult.LeadId);
        var hasPhone = !string.IsNullOrWhiteSpace(leadDetails?.Phone) || !string.IsNullOrWhiteSpace(journeyDetails?.PrimaryPhone);
        var hasEmail = !string.IsNullOrWhiteSpace(leadDetails?.Email) || !string.IsNullOrWhiteSpace(journeyDetails?.PrimaryEmail);
        var hasCity = !string.IsNullOrWhiteSpace(leadDetails?.City) || !string.IsNullOrWhiteSpace(journeyDetails?.Qualification.City);
        var hasServiceCategory = !string.IsNullOrWhiteSpace(leadDetails?.ServiceCategory) || !string.IsNullOrWhiteSpace(journeyDetails?.Qualification.NormalizedServiceCategoryName);
        var hasPostalCode = !string.IsNullOrWhiteSpace(leadDetails?.PostalCode) || !string.IsNullOrWhiteSpace(journeyDetails?.Qualification.PostalCode);
        var hasAddressDetails =
            !string.IsNullOrWhiteSpace(journeyDetails?.Qualification.Street) ||
            !string.IsNullOrWhiteSpace(journeyDetails?.Qualification.Neighborhood);
        var hasProblemContext = !string.IsNullOrWhiteSpace(journeyDetails?.Qualification.ProblemContext);
        var message = upsertResult.CreatedLead
            ? "Lead criado via automacao do bot Telegram."
            : "Lead atualizado via automacao do bot Telegram.";

        _logger.LogInformation(
            "Automacao Telegram processou conversa {ChatbotConversationId} no board {BoardType}. LeadId={LeadId}. CreatedLead={CreatedLead}. JourneyId={JourneyId}. ChatwootStatus={ChatwootStatus}.",
            request.ChatbotConversationId,
            upsertResult.BoardType,
            upsertResult.LeadId,
            upsertResult.CreatedLead,
            upsertResult.JourneyId,
            chatwootResult.Status);

        return TelegramLeadAutomationResult.Ok(new TelegramLeadAutomationResponse
        {
            Success = true,
            LeadId = upsertResult.LeadId,
            Created = upsertResult.CreatedLead,
            BoardType = upsertResult.BoardType,
            Message = message,
            HasPhone = hasPhone,
            HasEmail = hasEmail,
            HasCity = hasCity,
            HasServiceCategory = hasServiceCategory,
            HasPostalCode = hasPostalCode,
            HasAddressDetails = hasAddressDetails,
            HasProblemContext = hasProblemContext,
            QualificationStatus = qualification.Status,
            ConfirmationPrompt = qualification.ConfirmationPrompt,
            MissingRequiredFields = qualification.MissingRequiredFields,
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
