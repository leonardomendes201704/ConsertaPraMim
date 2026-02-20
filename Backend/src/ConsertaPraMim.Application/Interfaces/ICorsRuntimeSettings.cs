using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ICorsRuntimeSettings
{
    Task<AdminCorsRuntimeConfigDto> GetCorsConfigAsync(
        CancellationToken cancellationToken = default);

    bool IsOriginAllowed(string? origin);

    void InvalidateCorsCache();
}
