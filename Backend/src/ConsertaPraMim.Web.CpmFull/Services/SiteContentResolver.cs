namespace AppMobileCPM.Services;

public sealed class SiteContentResolver : ISiteContentResolver
{
    private const string CacheKey = "__cpm_site_content_cache";

    private readonly IMarketplaceRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly object _fallbackLock = new();
    private IReadOnlyDictionary<string, string>? _fallbackCache;

    public SiteContentResolver(IMarketplaceRepository repository, IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.Items.TryGetValue(CacheKey, out var cached) &&
                cached is IReadOnlyDictionary<string, string> cachedItems)
            {
                return cachedItems;
            }

            var items = _repository.GetSiteContents();
            httpContext.Items[CacheKey] = items;
            return items;
        }

        if (_fallbackCache is not null)
        {
            return _fallbackCache;
        }

        lock (_fallbackLock)
        {
            _fallbackCache ??= _repository.GetSiteContents();
        }

        return _fallbackCache;
    }

    public string Get(string key, string fallbackValue)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallbackValue;
        }

        var contents = GetAll();
        if (contents.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallbackValue;
    }
}
