using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class MobilePushDeviceService : IMobilePushDeviceService
{
    private static readonly HashSet<string> AllowedAppKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "client",
        "provider"
    };

    private static readonly HashSet<string> AllowedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "android",
        "ios",
        "web"
    };

    private readonly IMobilePushDeviceRepository _mobilePushDeviceRepository;
    private readonly IUserRepository _userRepository;

    public MobilePushDeviceService(
        IMobilePushDeviceRepository mobilePushDeviceRepository,
        IUserRepository userRepository)
    {
        _mobilePushDeviceRepository = mobilePushDeviceRepository;
        _userRepository = userRepository;
    }

    public async Task<MobilePushDeviceRegistrationResponseDto> RegisterAsync(
        Guid userId,
        string appKind,
        MobilePushDeviceRegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("Usuario invalido para registro de push.");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("Usuario autenticado nao encontrado.");
        }

        var normalizedAppKind = NormalizeAppKind(appKind);
        var normalizedToken = NormalizeToken(request.Token);
        var normalizedPlatform = NormalizePlatform(request.Platform);
        var now = DateTime.UtcNow;

        var device = await _mobilePushDeviceRepository
            .GetByTokenAndAppKindAsync(normalizedToken, normalizedAppKind, cancellationToken);

        if (device == null)
        {
            device = new MobilePushDevice
            {
                UserId = userId,
                Token = normalizedToken,
                Platform = normalizedPlatform,
                AppKind = normalizedAppKind,
                DeviceId = NormalizeOptionalField(request.DeviceId, 200),
                DeviceModel = NormalizeOptionalField(request.DeviceModel, 200),
                OsVersion = NormalizeOptionalField(request.OsVersion, 100),
                AppVersion = NormalizeOptionalField(request.AppVersion, 64),
                IsActive = true,
                LastRegisteredAtUtc = now,
                LastFailureAtUtc = null,
                LastFailureReason = null,
                UpdatedAt = now
            };

            await _mobilePushDeviceRepository.AddAsync(device, cancellationToken);
        }
        else
        {
            device.UserId = userId;
            device.Platform = normalizedPlatform;
            device.DeviceId = NormalizeOptionalField(request.DeviceId, 200);
            device.DeviceModel = NormalizeOptionalField(request.DeviceModel, 200);
            device.OsVersion = NormalizeOptionalField(request.OsVersion, 100);
            device.AppVersion = NormalizeOptionalField(request.AppVersion, 64);
            device.IsActive = true;
            device.LastRegisteredAtUtc = now;
            device.LastFailureAtUtc = null;
            device.LastFailureReason = null;
            device.UpdatedAt = now;

            await _mobilePushDeviceRepository.UpdateAsync(device, cancellationToken);
        }

        var normalizedDeviceId = NormalizeOptionalField(request.DeviceId, 200);
        if (!string.IsNullOrWhiteSpace(normalizedDeviceId))
        {
            await _mobilePushDeviceRepository.DeactivateByUserAndDeviceIdExceptTokenAsync(
                userId,
                normalizedAppKind,
                normalizedDeviceId,
                normalizedToken,
                "device_token_replaced",
                cancellationToken);
        }

        return new MobilePushDeviceRegistrationResponseDto(
            device.Id,
            device.AppKind,
            device.Platform,
            device.IsActive,
            device.LastRegisteredAtUtc);
    }

    public async Task<MobilePushDeviceUnregisterResponseDto> UnregisterAsync(
        Guid userId,
        string appKind,
        MobilePushDeviceUnregisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("Usuario invalido para desregistro de push.");
        }

        var normalizedAppKind = NormalizeAppKind(appKind);
        var normalizedToken = NormalizeOptionalField(request.Token, 4096);
        var normalizedDeviceId = NormalizeOptionalField(request.DeviceId, 200);

        if (string.IsNullOrWhiteSpace(normalizedToken) && string.IsNullOrWhiteSpace(normalizedDeviceId))
        {
            throw new InvalidOperationException("Informe token ou deviceId para desregistrar notificacao push.");
        }

        var totalDeactivated = 0;

        if (!string.IsNullOrWhiteSpace(normalizedToken))
        {
            totalDeactivated += await _mobilePushDeviceRepository.DeactivateByUserAndTokenAsync(
                userId,
                normalizedAppKind,
                normalizedToken,
                "user_logout",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(normalizedDeviceId))
        {
            totalDeactivated += await _mobilePushDeviceRepository.DeactivateByUserAndDeviceIdExceptTokenAsync(
                userId,
                normalizedAppKind,
                normalizedDeviceId,
                keepToken: string.Empty,
                reason: "user_logout",
                cancellationToken);
        }

        return new MobilePushDeviceUnregisterResponseDto(
            totalDeactivated,
            normalizedAppKind,
            DateTime.UtcNow);
    }

    private static string NormalizeAppKind(string appKind)
    {
        var normalized = (appKind ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedAppKinds.Contains(normalized))
        {
            throw new InvalidOperationException("Canal mobile invalido para push.");
        }

        return normalized;
    }

    private static string NormalizePlatform(string platform)
    {
        var normalized = string.IsNullOrWhiteSpace(platform)
            ? "android"
            : platform.Trim().ToLowerInvariant();

        if (!AllowedPlatforms.Contains(normalized))
        {
            throw new InvalidOperationException("Plataforma de push invalida. Use android, ios ou web.");
        }

        return normalized;
    }

    private static string NormalizeToken(string token)
    {
        var normalized = NormalizeOptionalField(token, 4096);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Token de notificacao push e obrigatorio.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalField(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            return trimmed[..maxLength];
        }

        return trimmed;
    }
}
