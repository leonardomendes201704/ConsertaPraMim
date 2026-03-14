namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootApiClient
{
    Task<ChatwootConnectionCheckResult> CheckConnectionAsync(CancellationToken cancellationToken = default);
    Task<ChatwootContactSummary?> GetContactAsync(long contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatwootContactSummary>> SearchContactsAsync(string query, CancellationToken cancellationToken = default);
    Task<ChatwootContactSummary> CreateContactAsync(ChatwootUpsertContactRequest request, CancellationToken cancellationToken = default);
    Task<ChatwootContactSummary> UpdateContactAsync(long contactId, ChatwootUpsertContactRequest request, CancellationToken cancellationToken = default);
    Task<ChatwootContactInboxSummary> CreateContactInboxAsync(long contactId, ChatwootCreateContactInboxRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatwootConversationSummary>> ListContactConversationsAsync(long contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListContactLabelsAsync(long contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ReplaceContactLabelsAsync(long contactId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default);
    Task<ChatwootConversationSummary> CreateConversationAsync(ChatwootCreateConversationRequest request, CancellationToken cancellationToken = default);
    Task<ChatwootMessageSummary> CreateMessageAsync(long conversationId, ChatwootCreateMessageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListConversationLabelsAsync(long conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ReplaceConversationLabelsAsync(long conversationId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default);
    Task UpdateConversationCustomAttributesAsync(long conversationId, IReadOnlyDictionary<string, object?> customAttributes, CancellationToken cancellationToken = default);
    Task<string> UpdateConversationStatusAsync(long conversationId, string status, CancellationToken cancellationToken = default);
}
