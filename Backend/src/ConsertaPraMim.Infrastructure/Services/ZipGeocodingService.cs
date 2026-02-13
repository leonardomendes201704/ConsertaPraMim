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

        ViaCepResponse? viaCep;
        try
        {
            viaCep = await client.GetFromJsonAsync<ViaCepResponse>($"https://viacep.com.br/ws/{normalizedZip}/json/");
        }
        catch
        {
            return null;
        }

        if (viaCep == null || viaCep.Erro == true || string.IsNullOrWhiteSpace(viaCep.Localidade) || string.IsNullOrWhiteSpace(viaCep.Uf))
        {
            return null;
        }

        foreach (var query in BuildQueries(normalizedZip, viaCep, street, city))
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

            return (normalizedZip, latitude, longitude, viaCep.Logradouro, viaCep.Localidade);
        }

        return null;
    }

    private static IEnumerable<string> BuildQueries(string normalizedZip, ViaCepResponse viaCep, string? street, string? city)
    {
        var queries = new List<string>();
        var cityName = !string.IsNullOrWhiteSpace(city) ? city : viaCep.Localidade;
        var cityUf = $"{cityName}, {viaCep.Uf}, Brasil";

        if (!string.IsNullOrWhiteSpace(street))
        {
            queries.Add($"{street}, {cityUf}");
        }

        if (!string.IsNullOrWhiteSpace(viaCep.Logradouro))
        {
            queries.Add($"{viaCep.Logradouro}, {viaCep.Bairro}, {cityUf}");
        }

        if (!string.IsNullOrWhiteSpace(viaCep.Bairro))
        {
            queries.Add($"{viaCep.Bairro}, {cityUf}");
        }

        queries.Add($"{normalizedZip}, {cityUf}");
        queries.Add(cityUf);

        return queries
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct();
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

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }
    }
}
