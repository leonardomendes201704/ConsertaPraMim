using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramBotApiClient
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken cancellationToken);

    Task SendMessageAsync(long chatId, string? text, IReadOnlyList<StoredLocalFile> attachments, CancellationToken cancellationToken);

    Task<string?> GetFilePathAsync(string fileId, CancellationToken cancellationToken);

    Task DownloadFileAsync(string filePath, Stream destination, CancellationToken cancellationToken);
}
