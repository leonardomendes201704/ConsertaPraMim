namespace ConsertaPraMim.Application.DTOs;

public class MobilePushDeviceRegisterRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public string? InstallationId { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceModel { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? TimeZone { get; set; }
}

public class MobilePushDeviceUnregisterRequestDto
{
    public string? Token { get; set; }
    public string? InstallationId { get; set; }
    public string? DeviceId { get; set; }
}

public record MobilePushDeviceRegistrationResponseDto(
    Guid DeviceId,
    string AppKind,
    string Platform,
    bool IsActive,
    DateTime RegisteredAtUtc);

public record MobilePushDeviceUnregisterResponseDto(
    int DeactivatedDevices,
    string AppKind,
    DateTime ProcessedAtUtc);

public record MobilePushDeviceDiagnosticDto(
    Guid Id,
    Guid UserId,
    string AppKind,
    string Platform,
    string InstallationId,
    string? DeviceId,
    string? DeviceModel,
    string? OsVersion,
    string? AppVersion,
    string? TimeZone,
    bool IsActive,
    DateTime LastSeenAtUtc,
    DateTime LastRegisteredAtUtc,
    DateTime? LastDeliveredAtUtc,
    DateTime? LastFailureAtUtc,
    string? LastFailureReason,
    DateTime? RevokedAtUtc,
    string MaskedToken);
