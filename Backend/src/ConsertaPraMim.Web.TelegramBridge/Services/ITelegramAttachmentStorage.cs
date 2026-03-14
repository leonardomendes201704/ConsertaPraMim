using ConsertaPraMim.Web.TelegramBridge.Models;
using Microsoft.AspNetCore.Http;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramAttachmentStorage
{
    Task<IReadOnlyList<StoredLocalFile>> SavePanelFilesAsync(long chatId, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredLocalFile>> SaveIncomingTelegramFilesAsync(long chatId, TelegramMessage message, CancellationToken cancellationToken);

    int PurgeExpiredFiles(DateTime purgeBeforeUtc);
}
