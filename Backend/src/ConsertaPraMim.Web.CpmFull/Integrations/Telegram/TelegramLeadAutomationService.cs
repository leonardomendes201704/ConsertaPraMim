using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramLeadAutomationService : ITelegramLeadAutomationService
{
    public const string SharedSecretHeaderName = "X-Telegram-Automation-Key";

    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootLeadSyncService _chatwootLeadSyncService;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramLeadAutomationService> _logger;

    public TelegramLeadAutomationService(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramLeadAutomationService> logger)
    {
        _kanbanService = kanbanService;
        _chatwootLeadSyncService = chatwootLeadSyncService;
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

        var upsertResult = _kanbanService.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = normalizedBoardType,
            ChatbotConversationId = request.ChatbotConversationId,
            ChannelConversationId = request.ChannelConversationId,
            TelegramChatId = request.TelegramChatId,
            ClientId = request.UserId,
            ClientName = string.IsNullOrWhiteSpace(request.UserName)
                ? (normalizedBoardType == AdminKanbanBoardTypes.Providers ? "Prestador Telegram" : "Cliente Telegram")
                : request.UserName.Trim(),
            ClientPhone = request.UserPhone,
            ClientEmail = request.UserEmail,
            ServiceRequestId = request.ServiceRequestId,
            ServiceCategory = request.ServiceCategory,
            PostalCode = request.PostalCode,
            City = request.City,
            StatusNote = request.StatusNote,
            InternalNotes = request.InternalNotes,
            LastContactAt = request.LastContactAtUtc
        });

        var chatwootResult = await _chatwootLeadSyncService.SyncLeadAsync(upsertResult.LeadId, cancellationToken);
        var message = upsertResult.Created
            ? "Lead criado via automacao do bot Telegram."
            : "Lead atualizado via automacao do bot Telegram.";

        _logger.LogInformation(
            "Automacao Telegram processou conversa {ChatbotConversationId} no board {BoardType}. LeadId={LeadId}. Created={Created}. ChatwootStatus={ChatwootStatus}.",
            upsertResult.ChatbotConversationId,
            upsertResult.BoardType,
            upsertResult.LeadId,
            upsertResult.Created,
            chatwootResult.Status);

        return TelegramLeadAutomationResult.Ok(new TelegramLeadAutomationResponse
        {
            Success = true,
            LeadId = upsertResult.LeadId,
            Created = upsertResult.Created,
            BoardType = upsertResult.BoardType,
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
}
