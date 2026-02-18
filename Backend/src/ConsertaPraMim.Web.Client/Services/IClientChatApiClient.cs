using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Client.Services;

public interface IClientChatApiClient
{
    Task<(IReadOnlyList<ChatConversationSummaryDto> Conversations, string? ErrorMessage)> GetConversationsAsync(CancellationToken cancellationToken = default);
}
