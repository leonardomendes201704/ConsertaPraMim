using System.Diagnostics;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Infrastructure.Services;

public sealed class FireTvDashboardHealthProbe : IFireTvDashboardHealthProbe
{
    private readonly IHttpClientFactory _httpClientFactory;

    public FireTvDashboardHealthProbe(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<AdminFireTvHealthTargetStatusDto>> ProbeAsync(
        IReadOnlyList<FireTvDashboardHealthTargetConfigDto> targets,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        if (targets.Count == 0)
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 500, 10000));

        var output = new List<AdminFireTvHealthTargetStatusDto>(targets.Count);
        foreach (var target in targets)
        {
            output.Add(await ProbeTargetAsync(client, target, cancellationToken));
        }

        return output;
    }

    private static async Task<AdminFireTvHealthTargetStatusDto> ProbeTargetAsync(
        HttpClient client,
        FireTvDashboardHealthTargetConfigDto target,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, target.Url);
            request.Headers.Accept.ParseAdd("*/*");

            var stopwatch = Stopwatch.StartNew();
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            stopwatch.Stop();

            var healthy = (int)response.StatusCode < 500;
            var statusLabel = healthy ? "OK" : $"HTTP {(int)response.StatusCode}";

            return new AdminFireTvHealthTargetStatusDto(
                target.Key,
                target.Label,
                target.Url,
                healthy,
                (int)stopwatch.ElapsedMilliseconds,
                statusLabel,
                healthy ? null : response.ReasonPhrase);
        }
        catch (OperationCanceledException)
        {
            return new AdminFireTvHealthTargetStatusDto(
                target.Key,
                target.Label,
                target.Url,
                false,
                null,
                "Timeout",
                "Tempo limite excedido.");
        }
        catch (Exception ex)
        {
            return new AdminFireTvHealthTargetStatusDto(
                target.Key,
                target.Label,
                target.Url,
                false,
                null,
                "Offline",
                ex.Message);
        }
    }
}
