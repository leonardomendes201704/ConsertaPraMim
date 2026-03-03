namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed record StoredLocalFile(
    string PhysicalPath,
    string RelativeUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind);
