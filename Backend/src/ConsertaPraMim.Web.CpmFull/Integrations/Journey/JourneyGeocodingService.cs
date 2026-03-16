using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyGeocodingService : IJourneyGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public JourneyGeocodingService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JourneyGeocodingResult?> ResolveAsync(
        string? postalCode,
        string? street = null,
        string? city = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedZip = NormalizeZip(postalCode);
        if (string.IsNullOrWhiteSpace(normalizedZip))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        BrasilApiCepResponse? brasilApi = null;
        try
        {
            brasilApi = await client.GetFromJsonAsync<BrasilApiCepResponse>(
                $"https://brasilapi.com.br/api/cep/v2/{normalizedZip}",
                cancellationToken);
        }
        catch
        {
            brasilApi = null;
        }

        if (TryParseInvariantDouble(brasilApi?.Location?.Coordinates?.Latitude, out var brasilLatitude) &&
            TryParseInvariantDouble(brasilApi?.Location?.Coordinates?.Longitude, out var brasilLongitude))
        {
            return new JourneyGeocodingResult
            {
                PostalCode = FormatZip(normalizedZip),
                Latitude = brasilLatitude,
                Longitude = brasilLongitude,
                Street = FirstNonEmpty(street, brasilApi?.Street) ?? string.Empty,
                Neighborhood = brasilApi?.Neighborhood ?? string.Empty,
                City = FirstNonEmpty(city, brasilApi?.City) ?? string.Empty,
                State = brasilApi?.State ?? string.Empty
            };
        }

        AwesomeApiCepResponse? awesomeApi = null;
        try
        {
            awesomeApi = await client.GetFromJsonAsync<AwesomeApiCepResponse>(
                $"https://cep.awesomeapi.com.br/json/{normalizedZip}",
                cancellationToken);
        }
        catch
        {
            awesomeApi = null;
        }

        if (TryParseInvariantDouble(awesomeApi?.Lat, out var awesomeLatitude) &&
            TryParseInvariantDouble(awesomeApi?.Lng, out var awesomeLongitude))
        {
            return new JourneyGeocodingResult
            {
                PostalCode = FormatZip(normalizedZip),
                Latitude = awesomeLatitude,
                Longitude = awesomeLongitude,
                Street = FirstNonEmpty(street, awesomeApi?.Address, awesomeApi?.AddressName) ?? string.Empty,
                City = FirstNonEmpty(city, awesomeApi?.City) ?? string.Empty,
                State = string.Empty
            };
        }

        ViaCepResponse? viaCep = null;
        try
        {
            viaCep = await client.GetFromJsonAsync<ViaCepResponse>(
                $"https://viacep.com.br/ws/{normalizedZip}/json/",
                cancellationToken);
        }
        catch
        {
            viaCep = null;
        }

        if (viaCep?.Erro == true)
        {
            viaCep = null;
        }

        var resolvedCity = FirstNonEmpty(city, viaCep?.Localidade, brasilApi?.City);
        var resolvedState = FirstNonEmpty(viaCep?.Uf, brasilApi?.State);
        var resolvedStreet = FirstNonEmpty(street, viaCep?.Logradouro, brasilApi?.Street);
        var resolvedNeighborhood = FirstNonEmpty(viaCep?.Bairro, brasilApi?.Neighborhood);

        if (string.IsNullOrWhiteSpace(resolvedCity) || string.IsNullOrWhiteSpace(resolvedState))
        {
            return null;
        }

        foreach (var query in BuildQueries(normalizedZip, resolvedStreet, resolvedNeighborhood, resolvedCity, resolvedState))
        {
            var encodedQuery = Uri.EscapeDataString(query);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&q={encodedQuery}");
            request.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.CpmFull/1.0");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            var results = await response.Content.ReadFromJsonAsync<List<NominatimResult>>(cancellationToken: cancellationToken);
            var first = results?.FirstOrDefault();
            if (first is null ||
                !double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                continue;
            }

            return new JourneyGeocodingResult
            {
                PostalCode = FormatZip(normalizedZip),
                Latitude = latitude,
                Longitude = longitude,
                Street = resolvedStreet ?? string.Empty,
                Neighborhood = resolvedNeighborhood ?? string.Empty,
                City = resolvedCity,
                State = resolvedState
            };
        }

        return null;
    }

    private static IEnumerable<string> BuildQueries(
        string normalizedZip,
        string? street,
        string? neighborhood,
        string city,
        string state)
    {
        var queries = new List<string>();
        var cityState = $"{city}, {state}, Brasil";

        if (!string.IsNullOrWhiteSpace(street))
        {
            queries.Add($"{street}, {cityState}");
        }

        if (!string.IsNullOrWhiteSpace(neighborhood))
        {
            queries.Add($"{neighborhood}, {cityState}");
        }

        queries.Add($"{normalizedZip}, {cityState}");
        queries.Add(cityState);

        return queries.Where(query => !string.IsNullOrWhiteSpace(query)).Distinct();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static bool TryParseInvariantDouble(string? rawValue, out double result)
    {
        result = 0;
        return !string.IsNullOrWhiteSpace(rawValue) &&
               double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static string? NormalizeZip(string? zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return null;
        }

        var digits = new string(zipCode.Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    private static string FormatZip(string normalizedZip) =>
        normalizedZip.Length == 8
            ? $"{normalizedZip[..5]}-{normalizedZip[5..]}"
            : normalizedZip;

    private sealed class ViaCepResponse
    {
        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("localidade")]
        public string? Localidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }

        [JsonPropertyName("erro")]
        public bool? Erro { get; set; }
    }

    private sealed class BrasilApiCepResponse
    {
        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("neighborhood")]
        public string? Neighborhood { get; set; }

        [JsonPropertyName("street")]
        public string? Street { get; set; }

        [JsonPropertyName("location")]
        public BrasilApiLocation? Location { get; set; }
    }

    private sealed class BrasilApiLocation
    {
        [JsonPropertyName("coordinates")]
        public BrasilApiCoordinates? Coordinates { get; set; }
    }

    private sealed class BrasilApiCoordinates
    {
        [JsonPropertyName("longitude")]
        public string? Longitude { get; set; }

        [JsonPropertyName("latitude")]
        public string? Latitude { get; set; }
    }

    private sealed class AwesomeApiCepResponse
    {
        [JsonPropertyName("address_name")]
        public string? AddressName { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lng")]
        public string? Lng { get; set; }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }
    }
}
