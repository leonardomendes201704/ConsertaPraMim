using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramAttachmentStorage : ITelegramAttachmentStorage
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ILogger<TelegramAttachmentStorage> _logger;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly string _webRootPath;
    private readonly string _uploadsRootPath;
    private readonly long _maxAttachmentBytes;

    public TelegramAttachmentStorage(
        ITelegramBotApiClient telegramBotApiClient,
        IWebHostEnvironment hostEnvironment,
        IOptions<TelegramBridgeOptions> options,
        ILogger<TelegramAttachmentStorage> logger)
    {
        _telegramBotApiClient = telegramBotApiClient;
        _logger = logger;
        _maxAttachmentBytes = Math.Max(1_024_000, options.Value.MaxAttachmentBytes);
        _webRootPath = string.IsNullOrWhiteSpace(hostEnvironment.WebRootPath)
            ? Path.Combine(hostEnvironment.ContentRootPath, "wwwroot")
            : hostEnvironment.WebRootPath;
        _uploadsRootPath = Path.Combine(_webRootPath, "uploads", "telegram-bridge");
    }

    public async Task<IReadOnlyList<StoredLocalFile>> SavePanelFilesAsync(
        long chatId,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var targetDirectory = BuildRelativeDirectory(chatId, now);
        var targetAbsoluteDirectory = Path.Combine(_webRootPath, targetDirectory);
        Directory.CreateDirectory(targetAbsoluteDirectory);

        var savedFiles = new List<StoredLocalFile>(files.Count);

        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                continue;
            }

            if (file.Length > _maxAttachmentBytes)
            {
                throw new InvalidOperationException(
                    $"Arquivo '{file.FileName}' excede o limite de {_maxAttachmentBytes / (1024 * 1024)} MB.");
            }

            var safeFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeFileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var relativePath = Path.Combine(targetDirectory, storedFileName);
            var absolutePath = Path.Combine(_webRootPath, relativePath);

            await using (var destination = File.Create(absolutePath))
            {
                await using var source = file.OpenReadStream();
                await source.CopyToAsync(destination, cancellationToken);
            }

            var normalizedRelativeUrl = "/" + relativePath.Replace('\\', '/');
            var contentType = ResolveContentType(absolutePath, file.ContentType);
            var mediaKind = ResolveMediaKind(contentType, safeFileName);

            savedFiles.Add(new StoredLocalFile(
                PhysicalPath: absolutePath,
                RelativeUrl: normalizedRelativeUrl,
                FileName: safeFileName,
                ContentType: contentType,
                SizeBytes: file.Length,
                MediaKind: mediaKind));
        }

        return savedFiles;
    }

    public async Task<IReadOnlyList<StoredLocalFile>> SaveIncomingTelegramFilesAsync(
        long chatId,
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        if (!_telegramBotApiClient.IsConfigured)
        {
            return [];
        }

        var candidates = BuildCandidates(message);
        if (candidates.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var targetDirectory = BuildRelativeDirectory(chatId, now);
        var targetAbsoluteDirectory = Path.Combine(_webRootPath, targetDirectory);
        Directory.CreateDirectory(targetAbsoluteDirectory);

        var results = new List<StoredLocalFile>(candidates.Count);

        foreach (var candidate in candidates)
        {
            try
            {
                var filePath = await _telegramBotApiClient.GetFilePathAsync(candidate.FileId, cancellationToken);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                var extension = ResolveExtension(candidate.FileName, filePath, candidate.MediaKind);
                var storedFileName = $"{Guid.NewGuid():N}{extension}";
                var relativePath = Path.Combine(targetDirectory, storedFileName);
                var absolutePath = Path.Combine(_webRootPath, relativePath);

                await using (var destination = File.Create(absolutePath))
                {
                    await _telegramBotApiClient.DownloadFileAsync(filePath, destination, cancellationToken);
                }

                var info = new FileInfo(absolutePath);
                var contentType = ResolveContentType(absolutePath, candidate.ContentType);

                results.Add(new StoredLocalFile(
                    PhysicalPath: absolutePath,
                    RelativeUrl: "/" + relativePath.Replace('\\', '/'),
                    FileName: candidate.FileName,
                    ContentType: contentType,
                    SizeBytes: info.Exists ? info.Length : 0,
                    MediaKind: candidate.MediaKind));
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Falha ao baixar anexo do Telegram. ChatId: {ChatId}, FileId: {FileId}",
                    TelegramSecuritySanitizer.MaskChatId(chatId),
                    candidate.FileId);
            }
        }

        return results;
    }

    public int PurgeExpiredFiles(DateTime purgeBeforeUtc)
    {
        if (!Directory.Exists(_uploadsRootPath))
        {
            return 0;
        }

        var normalizedPurgeBeforeUtc = purgeBeforeUtc.Kind == DateTimeKind.Utc
            ? purgeBeforeUtc
            : purgeBeforeUtc.ToUniversalTime();
        var deletedFiles = 0;

        foreach (var filePath in Directory.EnumerateFiles(_uploadsRootPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(filePath) >= normalizedPurgeBeforeUtc)
                {
                    continue;
                }

                File.Delete(filePath);
                deletedFiles++;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Falha ao remover anexo Telegram expirado. File={FileName}",
                    Path.GetFileName(filePath));
            }
        }

        DeleteEmptyDirectories(_uploadsRootPath);
        return deletedFiles;
    }

    private static List<IncomingFileCandidate> BuildCandidates(TelegramMessage message)
    {
        var candidates = new List<IncomingFileCandidate>();

        if (message.Photo is { Count: > 0 })
        {
            var bestPhoto = message.Photo
                .OrderByDescending(photo => photo.Width * photo.Height)
                .ThenByDescending(photo => photo.FileSize ?? 0)
                .First();

            var bestPhotoName = string.IsNullOrWhiteSpace(bestPhoto.FileUniqueId)
                ? $"photo-{bestPhoto.FileId}.jpg"
                : $"photo-{bestPhoto.FileUniqueId}.jpg";

            candidates.Add(new IncomingFileCandidate(bestPhoto.FileId, bestPhotoName, "image/jpeg", "image"));
        }

        if (message.Document is not null && !string.IsNullOrWhiteSpace(message.Document.FileId))
        {
            var fileName = string.IsNullOrWhiteSpace(message.Document.FileName)
                ? $"document-{message.Document.FileUniqueId ?? message.Document.FileId}.bin"
                : message.Document.FileName;
            candidates.Add(new IncomingFileCandidate(message.Document.FileId, fileName, message.Document.MimeType, "document"));
        }

        if (message.Video is not null && !string.IsNullOrWhiteSpace(message.Video.FileId))
        {
            var fileName = $"video-{message.Video.FileUniqueId ?? message.Video.FileId}.mp4";
            candidates.Add(new IncomingFileCandidate(message.Video.FileId, fileName, message.Video.MimeType, "video"));
        }

        if (message.Audio is not null && !string.IsNullOrWhiteSpace(message.Audio.FileId))
        {
            var fileName = string.IsNullOrWhiteSpace(message.Audio.FileName)
                ? $"audio-{message.Audio.FileUniqueId ?? message.Audio.FileId}.ogg"
                : message.Audio.FileName;
            candidates.Add(new IncomingFileCandidate(message.Audio.FileId, fileName, message.Audio.MimeType, "document"));
        }

        if (message.Voice is not null && !string.IsNullOrWhiteSpace(message.Voice.FileId))
        {
            var fileName = $"voice-{message.Voice.FileUniqueId ?? message.Voice.FileId}.ogg";
            candidates.Add(new IncomingFileCandidate(message.Voice.FileId, fileName, message.Voice.MimeType, "document"));
        }

        return candidates;
    }

    private static string BuildRelativeDirectory(long chatId, DateTime utcNow)
    {
        return Path.Combine(
            "uploads",
            "telegram-bridge",
            chatId.ToString(),
            utcNow.ToString("yyyy"),
            utcNow.ToString("MM"),
            utcNow.ToString("dd"));
    }

    private string ResolveContentType(string absolutePath, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return _contentTypeProvider.TryGetContentType(absolutePath, out var inferred)
            ? inferred
            : "application/octet-stream";
    }

    private static string ResolveExtension(string fileName, string filePath, string mediaKind)
    {
        var fromName = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(fromName))
        {
            return fromName;
        }

        var fromPath = Path.GetExtension(filePath);
        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return fromPath;
        }

        return mediaKind switch
        {
            "image" => ".jpg",
            "video" => ".mp4",
            _ => ".bin"
        };
    }

    private static string ResolveMediaKind(string contentType, string fileName)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webm", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        return "document";
    }

    private static void DeleteEmptyDirectories(string rootPath)
    {
        foreach (var directory in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
            {
                continue;
            }

            Directory.Delete(directory, recursive: false);
        }
    }

    private sealed record IncomingFileCandidate(string FileId, string FileName, string? ContentType, string MediaKind);
}
