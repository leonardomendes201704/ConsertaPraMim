using ConsertaPraMim.Application.Interfaces;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ConsertaPraMim.Infrastructure.Services;

public class ZipGeocodingService : IZipGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ZipGeocodingService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(string NormalizedZip, double Latitude, double Longitude, string? Street, string? City)?> ResolveCoordinatesAsync(
        string? zipCode,
        string? street = null,
        string? city = null)
    {
        var normalizedZip = NormalizeZip(zipCode);
        if (string.IsNullOrWhiteSpace(normalizedZip))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        BrasilApiCepResponse? brasilApi = null;
        try
        {
            brasilApi = await client.GetFromJsonAsync<BrasilApiCepResponse>($"https://brasilapi.com.br/api/cep/v2/{normalizedZip}");
        }
        catch
        {
            brasilApi = null;
        }

        if (TryParseInvariantDouble(brasilApi?.Location?.Coordinates?.Latitude, out var brasilLatitude) &&
            TryParseInvariantDouble(brasilApi?.Location?.Coordinates?.Longitude, out var brasilLongitude))
        {
            return (
                normalizedZip,
                brasilLatitude,
                brasilLongitude,
                FirstNonEmpty(street, brasilApi?.Street),
                FirstNonEmpty(city, brasilApi?.City));
        }

        AwesomeApiCepResponse? awesomeApi = null;
        try
        {
            awesomeApi = await client.GetFromJsonAsync<AwesomeApiCepResponse>($"https://cep.awesomeapi.com.br/json/{normalizedZip}");
        }
        catch
        {
            awesomeApi = null;
        }

        if (TryParseInvariantDouble(awesomeApi?.Lat, out var awesomeLatitude) &&
            TryParseInvariantDouble(awesomeApi?.Lng, out var awesomeLongitude))
        {
            return (
                normalizedZip,
                awesomeLatitude,
                awesomeLongitude,
                FirstNonEmpty(street, awesomeApi?.Address, awesomeApi?.AddressName),
                FirstNonEmpty(city, awesomeApi?.City));
        }

        ViaCepResponse? viaCep;
        try
        {
            viaCep = await client.GetFromJsonAsync<ViaCepResponse>($"https://viacep.com.br/ws/{normalizedZip}/json/");
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
        var resolvedUf = FirstNonEmpty(viaCep?.Uf, brasilApi?.State);
        var resolvedStreet = FirstNonEmpty(street, viaCep?.Logradouro, brasilApi?.Street);
        var resolvedNeighborhood = FirstNonEmpty(viaCep?.Bairro, brasilApi?.Neighborhood);

        if (string.IsNullOrWhiteSpace(resolvedCity) || string.IsNullOrWhiteSpace(resolvedUf))
        {
            return null;
        }

        foreach (var query in BuildQueries(normalizedZip, resolvedStreet, resolvedNeighborhood, resolvedCity, resolvedUf))
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&q={encodedQuery}");
            request.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim/1.0 (local-dev)");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch
            {
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            var results = await response.Content.ReadFromJsonAsync<List<NominatimResult>>();
            var first = results?.FirstOrDefault();
            if (first == null)
            {
                continue;
            }

            if (!double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
            {
                continue;
            }

            if (!double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                continue;
            }

            return (normalizedZip, latitude, longitude, resolvedStreet, resolvedCity);
        }

        return null;
    }

    public async Task<(string NormalizedZip, string? Street, string? City)?> ResolveAddressByCoordinatesAsync(
        double latitude,
        double longitude)
    {
        if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude < -90 || latitude > 90)
        {
            return null;
        }

        if (double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude < -180 || longitude > 180)
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&addressdetails=1&lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}");
        request.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim/1.0 (local-dev)");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<NominatimReverseResponse>();
        var rawZip = payload?.Address?.Postcode;
        var normalizedZip = NormalizeZip(rawZip);
        if (string.IsNullOrWhiteSpace(normalizedZip))
        {
            return null;
        }

        var resolvedStreet = payload?.Address?.Road
            ?? payload?.Address?.Pedestrian
            ?? payload?.Address?.Neighbourhood
            ?? payload?.Address?.Suburb;
        var resolvedCity = payload?.Address?.City
            ?? payload?.Address?.Town
            ?? payload?.Address?.Village
            ?? payload?.Address?.Municipality
            ?? payload?.Address?.County;

        return (normalizedZip, resolvedStreet, resolvedCity);
    }

    private static IEnumerable<string> BuildQueries(
        string normalizedZip,
        string? street,
        string? neighborhood,
        string city,
        string uf)
    {
        var queries = new List<string>();
        var cityUf = $"{city}, {uf}, Brasil";

        if (!string.IsNullOrWhiteSpace(street))
        {
            queries.Add($"{street}, {cityUf}");
        }

        if (!string.IsNullOrWhiteSpace(neighborhood))
        {
            queries.Add($"{neighborhood}, {cityUf}");
        }

        queries.Add($"{normalizedZip}, {cityUf}");
        queries.Add(cityUf);

        return queries
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryParseInvariantDouble(string? rawValue, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
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
        [JsonPropertyName("address_type")]
        public string? AddressType { get; set; }

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

    private sealed class NominatimReverseResponse
    {
        [JsonPropertyName("address")]
        public NominatimReverseAddress? Address { get; set; }
    }

    private sealed class NominatimReverseAddress
    {
        [JsonPropertyName("postcode")]
        public string? Postcode { get; set; }

        [JsonPropertyName("road")]
        public string? Road { get; set; }

        [JsonPropertyName("pedestrian")]
        public string? Pedestrian { get; set; }

        [JsonPropertyName("neighbourhood")]
        public string? Neighbourhood { get; set; }

        [JsonPropertyName("suburb")]
        public string? Suburb { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("village")]
        public string? Village { get; set; }

        [JsonPropertyName("municipality")]
        public string? Municipality { get; set; }

        [JsonPropertyName("county")]
        public string? County { get; set; }
    }
}
