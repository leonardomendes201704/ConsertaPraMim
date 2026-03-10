using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class TelegramChatbotConversationService : ITelegramChatbotConversationService
{
    private readonly IChatbotConversationRepository _chatbotConversationRepository;

    public TelegramChatbotConversationService(IChatbotConversationRepository chatbotConversationRepository)
    {
        _chatbotConversationRepository = chatbotConversationRepository;
    }

    public async Task<TelegramChatbotConversationDto> OpenOrResumeConversationAsync(TelegramChatbotOpenConversationRequestDto request)
    {
        ValidateClientId(request.ClientId);

        var channel = NormalizeRequired(request.Channel, 32, nameof(request.Channel));
        var channelConversationId = NormalizeRequired(request.ChannelConversationId, 128, nameof(request.ChannelConversationId));
        var interactionAtUtc = NormalizeUtc(request.InteractionAtUtc ?? DateTime.UtcNow);

        var conversation = await _chatbotConversationRepository.GetByClientAndChannelAsync(
            request.ClientId,
            channel,
            channelConversationId);

        if (conversation == null)
        {
            var createdConversation = new ChatbotConversation
            {
                ClientId = request.ClientId,
                Channel = channel,
                ChannelConversationId = channelConversationId,
                Status = request.Status,
                LastIntent = NormalizeOptional(request.LastIntent, 120),
                LastStep = NormalizeOptional(request.LastStep, 120),
                MetadataJson = NormalizeOptional(request.MetadataJson, 4000),
                StartedAtUtc = interactionAtUtc,
                LastInteractionAtUtc = interactionAtUtc
            };

            await _chatbotConversationRepository.AddConversationAsync(createdConversation);
            return MapConversation(createdConversation);
        }

        var hasChanges = false;
        if (conversation.Status != request.Status)
        {
            conversation.Status = request.Status;
            hasChanges = true;
        }

        if (request.LastIntent is not null)
        {
            conversation.LastIntent = NormalizeOptional(request.LastIntent, 120);
            hasChanges = true;
        }

        if (request.LastStep is not null)
        {
            conversation.LastStep = NormalizeOptional(request.LastStep, 120);
            hasChanges = true;
        }

        if (request.MetadataJson is not null)
        {
            conversation.MetadataJson = NormalizeOptional(request.MetadataJson, 4000);
            hasChanges = true;
        }

        var updatedLastInteraction = MaxUtc(conversation.LastInteractionAtUtc, interactionAtUtc);
        if (updatedLastInteraction != conversation.LastInteractionAtUtc)
        {
            conversation.LastInteractionAtUtc = updatedLastInteraction;
            hasChanges = true;
        }

        if (hasChanges)
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            await _chatbotConversationRepository.UpdateConversationAsync(conversation);
        }

        return MapConversation(conversation);
    }

    public async Task<TelegramChatbotConversationDto?> GetConversationAsync(Guid conversationId, Guid clientId)
    {
        ValidateClientId(clientId);
        if (conversationId == Guid.Empty)
        {
            return null;
        }

        var conversation = await _chatbotConversationRepository.GetByIdAsync(conversationId);
        if (conversation == null || conversation.ClientId != clientId)
        {
            return null;
        }

        return MapConversation(conversation);
    }

    public async Task<TelegramChatbotConversationHistoryDto?> GetConversationHistoryAsync(
        Guid conversationId,
        Guid clientId,
        int messageTake = 50,
        int snapshotTake = 20,
        int actionTake = 20)
    {
        var conversation = await GetConversationAsync(conversationId, clientId);
        if (conversation == null)
        {
            return null;
        }

        var messages = await _chatbotConversationRepository.GetMessagesAsync(conversationId, messageTake);
        var snapshots = await _chatbotConversationRepository.GetContextSnapshotsAsync(conversationId, snapshotTake);
        var actionLogs = await _chatbotConversationRepository.GetActionLogsAsync(conversationId, actionTake);

        return new TelegramChatbotConversationHistoryDto(
            conversation,
            messages.Select(MapMessage).ToList(),
            snapshots.Select(MapContextSnapshot).ToList(),
            actionLogs.Select(MapActionLog).ToList());
    }

    public async Task<TelegramChatbotConversationDto?> UpdateConversationStateAsync(TelegramChatbotUpdateConversationStateRequestDto request)
    {
        ValidateClientId(request.ClientId);
        if (request.ConversationId == Guid.Empty)
        {
            return null;
        }

        var conversation = await _chatbotConversationRepository.GetByIdForUpdateAsync(request.ConversationId);
        if (conversation == null || conversation.ClientId != request.ClientId)
        {
            return null;
        }

        var hasChanges = false;

        if (request.Status.HasValue && conversation.Status != request.Status.Value)
        {
            conversation.Status = request.Status.Value;
            hasChanges = true;
        }

        if (request.LastIntent is not null)
        {
            conversation.LastIntent = NormalizeOptional(request.LastIntent, 120);
            hasChanges = true;
        }

        if (request.LastStep is not null)
        {
            conversation.LastStep = NormalizeOptional(request.LastStep, 120);
            hasChanges = true;
        }

        if (request.MetadataJson is not null)
        {
            conversation.MetadataJson = NormalizeOptional(request.MetadataJson, 4000);
            hasChanges = true;
        }

        if (request.InteractionAtUtc.HasValue)
        {
            var interactionAt = NormalizeUtc(request.InteractionAtUtc.Value);
            var updatedLastInteraction = MaxUtc(conversation.LastInteractionAtUtc, interactionAt);
            if (updatedLastInteraction != conversation.LastInteractionAtUtc)
            {
                conversation.LastInteractionAtUtc = updatedLastInteraction;
                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return MapConversation(conversation);
        }

        conversation.UpdatedAt = DateTime.UtcNow;
        await _chatbotConversationRepository.UpdateConversationAsync(conversation);
        return MapConversation(conversation);
    }

    public async Task<TelegramChatbotMessageDto?> RegisterMessageAsync(TelegramChatbotRegisterMessageRequestDto request)
    {
        ValidateClientId(request.ClientId);
        if (request.ConversationId == Guid.Empty)
        {
            return null;
        }

        var conversation = await _chatbotConversationRepository.GetByIdForUpdateAsync(request.ConversationId);
        if (conversation == null || conversation.ClientId != request.ClientId)
        {
            return null;
        }

        var source = NormalizeRequired(request.Source, 32, nameof(request.Source));
        var channelMessageId = NormalizeOptional(request.ChannelMessageId, 128);
        var content = NormalizeOptional(request.Content, 8000);
        var metadataJson = NormalizeOptional(request.MetadataJson, 4000);
        if (content is null && channelMessageId is null && metadataJson is null)
        {
            throw new InvalidOperationException("A mensagem precisa conter conteudo, ChannelMessageId ou metadata.");
        }

        var sentAtUtc = NormalizeUtc(request.SentAtUtc ?? DateTime.UtcNow);
        var message = new ChatbotMessage
        {
            ConversationId = conversation.Id,
            ClientId = request.ClientId,
            Direction = request.Direction,
            Source = source,
            ChannelMessageId = channelMessageId,
            Content = content,
            IntentName = NormalizeOptional(request.IntentName, 120),
            ModelName = NormalizeOptional(request.ModelName, 120),
            PromptTokens = ValidateToken(request.PromptTokens, nameof(request.PromptTokens)),
            CompletionTokens = ValidateToken(request.CompletionTokens, nameof(request.CompletionTokens)),
            TotalTokens = ValidateToken(request.TotalTokens, nameof(request.TotalTokens)),
            SentAtUtc = sentAtUtc,
            MetadataJson = metadataJson
        };

        conversation.LastInteractionAtUtc = MaxUtc(conversation.LastInteractionAtUtc, sentAtUtc);
        if (request.IntentName is not null)
        {
            conversation.LastIntent = NormalizeOptional(request.IntentName, 120);
        }

        if (request.LastStep is not null)
        {
            conversation.LastStep = NormalizeOptional(request.LastStep, 120);
        }

        conversation.UpdatedAt = DateTime.UtcNow;
        await _chatbotConversationRepository.AddMessageAsync(message);
        return MapMessage(message);
    }

    public async Task<TelegramChatbotContextSnapshotDto?> RegisterContextSnapshotAsync(
        TelegramChatbotRegisterContextSnapshotRequestDto request)
    {
        ValidateClientId(request.ClientId);
        if (request.ConversationId == Guid.Empty)
        {
            return null;
        }

        var conversation = await _chatbotConversationRepository.GetByIdForUpdateAsync(request.ConversationId);
        if (conversation == null || conversation.ClientId != request.ClientId)
        {
            return null;
        }

        var capturedAtUtc = NormalizeUtc(request.CapturedAtUtc ?? DateTime.UtcNow);
        var snapshot = new ChatbotContextSnapshot
        {
            ConversationId = conversation.Id,
            ClientId = request.ClientId,
            SnapshotType = NormalizeRequired(request.SnapshotType, 80, nameof(request.SnapshotType)),
            ContextJson = NormalizeRequired(request.ContextJson, 16000, nameof(request.ContextJson)),
            PromptVersion = NormalizeOptional(request.PromptVersion, 60),
            ModelName = NormalizeOptional(request.ModelName, 120),
            PromptTokens = ValidateToken(request.PromptTokens, nameof(request.PromptTokens)),
            CompletionTokens = ValidateToken(request.CompletionTokens, nameof(request.CompletionTokens)),
            TotalTokens = ValidateToken(request.TotalTokens, nameof(request.TotalTokens)),
            CapturedAtUtc = capturedAtUtc
        };

        conversation.LastInteractionAtUtc = MaxUtc(conversation.LastInteractionAtUtc, capturedAtUtc);
        if (request.IntentName is not null)
        {
            conversation.LastIntent = NormalizeOptional(request.IntentName, 120);
        }

        if (request.LastStep is not null)
        {
            conversation.LastStep = NormalizeOptional(request.LastStep, 120);
        }

        conversation.UpdatedAt = DateTime.UtcNow;
        await _chatbotConversationRepository.AddContextSnapshotAsync(snapshot);
        return MapContextSnapshot(snapshot);
    }

    public async Task<TelegramChatbotActionLogDto?> RegisterActionLogAsync(TelegramChatbotRegisterActionLogRequestDto request)
    {
        ValidateClientId(request.ClientId);
        if (request.ConversationId == Guid.Empty)
        {
            return null;
        }

        var conversation = await _chatbotConversationRepository.GetByIdForUpdateAsync(request.ConversationId);
        if (conversation == null || conversation.ClientId != request.ClientId)
        {
            return null;
        }

        var occurredAtUtc = NormalizeUtc(request.OccurredAtUtc ?? DateTime.UtcNow);
        var actionLog = new ChatbotActionLog
        {
            ConversationId = conversation.Id,
            ClientId = request.ClientId,
            ActionType = NormalizeRequired(request.ActionType, 80, nameof(request.ActionType)),
            Status = request.Status,
            IntentName = NormalizeOptional(request.IntentName, 120),
            PayloadJson = NormalizeOptional(request.PayloadJson, 16000),
            ResultJson = NormalizeOptional(request.ResultJson, 16000),
            ErrorCode = NormalizeOptional(request.ErrorCode, 80),
            ErrorMessage = NormalizeOptional(request.ErrorMessage, 1200),
            CorrelationId = NormalizeOptional(request.CorrelationId, 80),
            OccurredAtUtc = occurredAtUtc,
            MetadataJson = NormalizeOptional(request.MetadataJson, 4000)
        };

        conversation.LastInteractionAtUtc = MaxUtc(conversation.LastInteractionAtUtc, occurredAtUtc);
        if (request.IntentName is not null)
        {
            conversation.LastIntent = NormalizeOptional(request.IntentName, 120);
        }

        if (request.LastStep is not null)
        {
            conversation.LastStep = NormalizeOptional(request.LastStep, 120);
        }

        if (request.ConversationStatus.HasValue)
        {
            conversation.Status = request.ConversationStatus.Value;
        }

        conversation.UpdatedAt = DateTime.UtcNow;
        await _chatbotConversationRepository.AddActionLogAsync(actionLog);
        return MapActionLog(actionLog);
    }

    private static TelegramChatbotConversationDto MapConversation(ChatbotConversation conversation)
    {
        return new TelegramChatbotConversationDto(
            conversation.Id,
            conversation.ClientId,
            conversation.Channel,
            conversation.ChannelConversationId,
            conversation.Status,
            NormalizeUtc(conversation.StartedAtUtc),
            NormalizeUtc(conversation.LastInteractionAtUtc),
            conversation.LastIntent,
            conversation.LastStep,
            conversation.MetadataJson);
    }

    private static TelegramChatbotMessageDto MapMessage(ChatbotMessage message)
    {
        return new TelegramChatbotMessageDto(
            message.Id,
            message.ConversationId,
            message.ClientId,
            message.Direction,
            message.Source,
            message.ChannelMessageId,
            message.Content,
            message.IntentName,
            message.ModelName,
            message.PromptTokens,
            message.CompletionTokens,
            message.TotalTokens,
            NormalizeUtc(message.SentAtUtc),
            message.MetadataJson);
    }

    private static TelegramChatbotContextSnapshotDto MapContextSnapshot(ChatbotContextSnapshot snapshot)
    {
        return new TelegramChatbotContextSnapshotDto(
            snapshot.Id,
            snapshot.ConversationId,
            snapshot.ClientId,
            snapshot.SnapshotType,
            snapshot.ContextJson,
            snapshot.PromptVersion,
            snapshot.ModelName,
            snapshot.PromptTokens,
            snapshot.CompletionTokens,
            snapshot.TotalTokens,
            NormalizeUtc(snapshot.CapturedAtUtc));
    }

    private static TelegramChatbotActionLogDto MapActionLog(ChatbotActionLog actionLog)
    {
        return new TelegramChatbotActionLogDto(
            actionLog.Id,
            actionLog.ConversationId,
            actionLog.ClientId,
            actionLog.ActionType,
            actionLog.Status,
            actionLog.IntentName,
            actionLog.PayloadJson,
            actionLog.ResultJson,
            actionLog.ErrorCode,
            actionLog.ErrorMessage,
            actionLog.CorrelationId,
            NormalizeUtc(actionLog.OccurredAtUtc),
            actionLog.MetadataJson);
    }

    private static int? ValidateToken(int? token, string fieldName)
    {
        if (!token.HasValue)
        {
            return null;
        }

        if (token.Value < 0)
        {
            throw new InvalidOperationException($"{fieldName} nao pode ser negativo.");
        }

        return token;
    }

    private static void ValidateClientId(Guid clientId)
    {
        if (clientId == Guid.Empty)
        {
            throw new InvalidOperationException("ClientId invalido para operacao do chatbot.");
        }
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        var normalized = NormalizeOptional(value, maxLength);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} e obrigatorio.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }

    private static DateTime MaxUtc(DateTime current, DateTime candidate)
    {
        var normalizedCurrent = NormalizeUtc(current);
        var normalizedCandidate = NormalizeUtc(candidate);
        return normalizedCandidate > normalizedCurrent
            ? normalizedCandidate
            : normalizedCurrent;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
