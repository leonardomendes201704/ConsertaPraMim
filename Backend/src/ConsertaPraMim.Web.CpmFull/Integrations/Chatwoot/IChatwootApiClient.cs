namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootApiClient
{
    Task<ChatwootConnectionCheckResult> CheckConnectionAsync(CancellationToken cancellationToken = default);
    Task<ChatwootContactSummary?> GetContactAsync(long contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatwootContactSummary>> SearchContactsAsync(string query, CancellationToken cancellationToken = default);
    Task<ChatwootContactSummary> CreateContactAsync(ChatwootUpsertContactRequest request, CancellationToken cancellationToken = default);
    Task<ChatwootContactSummary> UpdateContactAsync(long contactId, ChatwootUpsertContactRequest request, CancellationToken cancellationToken = default);
    Task<ChatwootContactInboxSummary> CreateContactInboxAsync(long contactId, ChatwootCreateContactInboxRequest request, CancellationToken cancellationToken = default);
    Task<ChatwootConversationSummary> CreateConversationAsync(ChatwootCreateConversationRequest request, CancellationToken cancellationToken = default);
    Task<ChatwootMessageSummary> CreateMessageAsync(long conversationId, ChatwootCreateMessageRequest request, CancellationToken cancellationToken = default);
}
