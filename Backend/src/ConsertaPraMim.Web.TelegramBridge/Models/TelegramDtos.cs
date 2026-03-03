using System.Text.Json.Serialization;

namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed class TelegramApiEnvelope<T>
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("result")]
    public T? Result { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; init; }
}

public sealed class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; init; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }
}

public sealed class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }

    [JsonPropertyName("date")]
    public long DateUnix { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; init; } = new();

    [JsonPropertyName("from")]
    public TelegramUser? From { get; init; }

    [JsonPropertyName("photo")]
    public IReadOnlyList<TelegramPhotoSize>? Photo { get; init; }

    [JsonPropertyName("document")]
    public TelegramDocument? Document { get; init; }

    [JsonPropertyName("video")]
    public TelegramVideo? Video { get; init; }

    [JsonPropertyName("audio")]
    public TelegramAudio? Audio { get; init; }

    [JsonPropertyName("voice")]
    public TelegramVoice? Voice { get; init; }
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
}

public sealed class TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
}

public sealed class TelegramPhotoSize
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = string.Empty;

    [JsonPropertyName("file_unique_id")]
    public string? FileUniqueId { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }
}

public sealed class TelegramDocument
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = string.Empty;

    [JsonPropertyName("file_unique_id")]
    public string? FileUniqueId { get; init; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }
}

public sealed class TelegramVideo
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = string.Empty;

    [JsonPropertyName("file_unique_id")]
    public string? FileUniqueId { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }
}

public sealed class TelegramAudio
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = string.Empty;

    [JsonPropertyName("file_unique_id")]
    public string? FileUniqueId { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }
}

public sealed class TelegramVoice
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = string.Empty;

    [JsonPropertyName("file_unique_id")]
    public string? FileUniqueId { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }
}

public sealed class TelegramFileReference
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = string.Empty;

    [JsonPropertyName("file_unique_id")]
    public string? FileUniqueId { get; init; }

    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }
}
