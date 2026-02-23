using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.LoadTest.Wpf.Models;

namespace ConsertaPraMim.LoadTest.Wpf.Services;

internal sealed class VuSession : IDisposable
{
    private readonly LoadTestRunOptions _options;
    private readonly Action<string> _logger;
    private readonly HttpClient _client;
    private readonly Random _random;
    private readonly string _clientId;
    private readonly string? _tenantId;
    private readonly AuthAccount? _account;
    private readonly object _capturedIdsLock = new();
    private readonly List<string> _capturedOrderIds = [];

    private string? _accessToken;

    public VuSession(int vuIndex, LoadTestRunOptions options, Action<string> logger)
    {
        _options = options;
        _logger = logger;
        _random = new Random((options.Seed * 10000) + vuIndex);
        _clientId = $"LT-{options.ScenarioName.ToUpperInvariant()}-{vuIndex:0000}";
        _tenantId = PickTenantId();
        _account = PickAccount(vuIndex);

        var handler = new HttpClientHandler();
        if (options.InsecureTls)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        _client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds))
        };
    }

    public void Dispose() => _client.Dispose();

    public EndpointConfig ChooseEndpoint()
    {
        var endpoints = _options.Config.Endpoints;
        var totalWeight = endpoints.Sum(e => Math.Max(e.Weight, 0));
        if (totalWeight <= 0)
        {
            return endpoints[_random.Next(endpoints.Count)];
        }

        var point = _random.NextDouble() * totalWeight;
        var cumulative = 0d;
        foreach (var endpoint in endpoints)
        {
            cumulative += Math.Max(endpoint.Weight, 0);
            if (point <= cumulative)
            {
                return endpoint;
            }
        }

        return endpoints[^1];
    }

    public int NextThinkDelay()
    {
        var min = Math.Max(_options.ThinkMinMs, 0);
        var max = Math.Max(_options.ThinkMaxMs, min);
        return _random.Next(min, max + 1);
    }

    public async Task ExecuteRequestAsync(EndpointConfig endpoint, MetricsCollector metrics, CancellationToken cancellationToken)
    {
        var method = HttpMethod.Parse(string.IsNullOrWhiteSpace(endpoint.Method) ? "GET" : endpoint.Method.ToUpperInvariant());
        var correlationId = Guid.NewGuid().ToString();
        var endpointKey = endpoint.EndpointKey;
        var requestPath = ResolvePath(endpoint);
        var requestUri = BuildUrl(requestPath);
        var requiresAuth = string.Equals(endpoint.Auth, "bearer", StringComparison.OrdinalIgnoreCase);

        if (requiresAuth)
        {
            var loginOk = await EnsureAuthAsync(cancellationToken).ConfigureAwait(false);
            if (!loginOk)
            {
                metrics.Record(endpointKey, method.Method, requestPath, _clientId, correlationId, null, 0, "AuthFailed", "Falha ao autenticar conta de teste.", true);
                return;
            }
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add("X-Client-Id", _clientId);
        request.Headers.Add("X-Correlation-Id", correlationId);
        if (!string.IsNullOrWhiteSpace(_tenantId))
        {
            request.Headers.Add("X-Tenant-Id", _tenantId);
        }
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            if ((int)response.StatusCode >= 400)
            {
                metrics.Record(endpointKey, method.Method, requestPath, _clientId, correlationId, (int)response.StatusCode, durationMs, null, Truncate($"HTTP {(int)response.StatusCode}: {content}", 220), true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(endpoint.Capture) && !string.IsNullOrWhiteSpace(content))
            {
                CaptureIds(content);
            }

            metrics.Record(endpointKey, method.Method, requestPath, _clientId, correlationId, (int)response.StatusCode, durationMs, null, string.Empty, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.Record(endpointKey, method.Method, requestPath, _clientId, correlationId, null, stopwatch.Elapsed.TotalMilliseconds, ex.GetType().Name, Truncate(ex.Message, 220), true);
        }
    }

    private async Task<bool> EnsureAuthAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return true;
        }

        if (!_options.Config.Auth.Enabled || _account is null)
        {
            return false;
        }

        var loginPath = string.IsNullOrWhiteSpace(_options.Config.Auth.LoginPath) ? "/api/auth/login" : _options.Config.Auth.LoginPath;
        var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(loginPath));
        request.Headers.Add("X-Client-Id", _clientId);
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        request.Content = new StringContent(JsonSerializer.Serialize(new { email = _account.Email, password = _account.Password }), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode >= 400)
            {
                _logger($"[{_clientId}] login falhou: {(int)response.StatusCode}");
                return false;
            }

            var content = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            var tokenField = string.IsNullOrWhiteSpace(_options.Config.Auth.TokenField) ? "token" : _options.Config.Auth.TokenField;
            if (!doc.RootElement.TryGetProperty(tokenField, out var tokenElement))
            {
                _logger($"[{_clientId}] campo de token '{tokenField}' nao encontrado.");
                return false;
            }

            var tokenValue = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                return false;
            }

            _accessToken = tokenValue;
            return true;
        }
        catch (Exception ex)
        {
            _logger($"[{_clientId}] erro no login: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private string ResolvePath(EndpointConfig endpoint)
    {
        var shouldInjectError = !string.IsNullOrWhiteSpace(endpoint.InvalidPath) &&
                                _options.ErrorInjectionRatePercent > 0 &&
                                (_random.NextDouble() * 100.0) <= _options.ErrorInjectionRatePercent;

        var path = shouldInjectError ? endpoint.InvalidPath! : endpoint.Path;
        if (path.Contains("{orderId}", StringComparison.OrdinalIgnoreCase))
        {
            var orderId = TryPickOrderId();
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                return path.Replace("{orderId}", orderId, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(endpoint.FallbackPath))
            {
                return endpoint.FallbackPath!;
            }

            return path.Replace("{orderId}", Guid.Empty.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return path;
    }

    private string BuildUrl(string path)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var cleanPath = path.StartsWith('/') ? path : "/" + path;
        return baseUrl + cleanPath;
    }

    private string? PickTenantId()
    {
        if (_options.Config.TenantIds.Count == 0)
        {
            return null;
        }

        return _options.Config.TenantIds[_random.Next(_options.Config.TenantIds.Count)];
    }

    private AuthAccount? PickAccount(int vuIndex)
    {
        var accounts = _options.Config.Auth.Accounts;
        if (accounts.Count == 0)
        {
            return null;
        }

        return accounts[(vuIndex - 1) % accounts.Count];
    }

    private string? TryPickOrderId()
    {
        lock (_capturedIdsLock)
        {
            if (_capturedOrderIds.Count == 0)
            {
                return null;
            }

            return _capturedOrderIds[_random.Next(_capturedOrderIds.Count)];
        }
    }

    private void CaptureIds(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectIdCandidates(doc.RootElement, values);
            if (values.Count == 0)
            {
                return;
            }

            lock (_capturedIdsLock)
            {
                foreach (var value in values)
                {
                    if (!_capturedOrderIds.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        _capturedOrderIds.Add(value);
                    }
                }

                while (_capturedOrderIds.Count > 250)
                {
                    _capturedOrderIds.RemoveAt(0);
                }
            }
        }
        catch
        {
            // Best-effort capture.
        }
    }

    private static void CollectIdCandidates(JsonElement element, ISet<string> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        (property.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                         property.Name.Equals("orderId", StringComparison.OrdinalIgnoreCase)))
                    {
                        var value = property.Value.GetString();
                        if (Guid.TryParse(value, out var parsed))
                        {
                            target.Add(parsed.ToString());
                        }
                    }

                    CollectIdCandidates(property.Value, target);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectIdCandidates(item, target);
                }
                break;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}
