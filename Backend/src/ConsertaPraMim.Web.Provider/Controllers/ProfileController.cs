using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IReviewService _reviewService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ProfileController(
        IProfileService profileService,
        IFileStorageService fileStorageService,
        IReviewService reviewService,
        IHttpClientFactory httpClientFactory)
    {
        _profileService = profileService;
        _fileStorageService = fileStorageService;
        _reviewService = reviewService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);
        
        var profile = await _profileService.GetProfileAsync(userId);
        ViewBag.Reviews = await _reviewService.GetByProviderAsync(userId);
        return View(profile);
    }

    [HttpPost]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        if (file != null)
        {
            using (var stream = file.OpenReadStream())
            {
                var imageUrl = await _fileStorageService.SaveFileAsync(stream, file.FileName, "profiles");
                await _profileService.UpdateProfilePictureAsync(userId, imageUrl);
            }
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int radiusKm, string[] categories, string? baseZipCode)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var categoryList = (categories ?? Array.Empty<string>())
            .Select(c =>
            {
                if (Enum.TryParse<ServiceCategory>(c.Trim(), ignoreCase: true, out var category))
                {
                    return (valid: true, category);
                }

                return (valid: false, category: default(ServiceCategory));
            })
            .Where(x => x.valid)
            .Select(x => x.category)
            .Distinct()
            .ToList();

        if (!categoryList.Any())
        {
            TempData["Error"] = "Selecione pelo menos uma especialidade.";
            return RedirectToAction("Index");
        }

        var current = await _profileService.GetProfileAsync(userId);
        var latitude = current?.ProviderProfile?.BaseLatitude;
        var longitude = current?.ProviderProfile?.BaseLongitude;
        var zipToPersist = current?.ProviderProfile?.BaseZipCode;

        var normalizedZip = NormalizeZip(baseZipCode);
        if (!string.IsNullOrWhiteSpace(normalizedZip))
        {
            var resolved = await ResolveCoordinatesFromZipAsync(normalizedZip);
            if (resolved == null)
            {
                TempData["Error"] = "Nao foi possivel localizar esse CEP. Confira e tente novamente.";
                return RedirectToAction("Index");
            }

            latitude = resolved.Value.Latitude;
            longitude = resolved.Value.Longitude;
            zipToPersist = normalizedZip;
        }

        var dto = new UpdateProviderProfileDto(radiusKm, zipToPersist, latitude, longitude, categoryList);
        
        await _profileService.UpdateProviderProfileAsync(userId, dto);
        
        TempData["Success"] = "Perfil atualizado com sucesso!";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> ResolveZip(string zipCode)
    {
        var normalizedZip = NormalizeZip(zipCode);
        if (string.IsNullOrWhiteSpace(normalizedZip))
        {
            return BadRequest(new { message = "CEP invalido. Informe 8 digitos." });
        }

        var resolved = await ResolveCoordinatesFromZipAsync(normalizedZip);
        if (resolved == null)
        {
            return NotFound(new { message = "Nao foi possivel localizar esse CEP." });
        }

        return Json(new
        {
            zipCode = normalizedZip,
            latitude = resolved.Value.Latitude,
            longitude = resolved.Value.Longitude,
            address = resolved.Value.Address
        });
    }

    private async Task<(double Latitude, double Longitude, string Address)?> ResolveCoordinatesFromZipAsync(string normalizedZip)
    {
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

        var queries = BuildGeocodingQueries(normalizedZip, viaCep);
        foreach (var query in queries)
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

            return (latitude, longitude, first.DisplayName ?? query);
        }

        return null;
    }

    private static IEnumerable<string> BuildGeocodingQueries(string zipCode, ViaCepResponse viaCep)
    {
        var list = new List<string>();
        var cityUf = $"{viaCep.Localidade}, {viaCep.Uf}, Brasil";

        if (!string.IsNullOrWhiteSpace(viaCep.Logradouro))
        {
            var withStreet = $"{viaCep.Logradouro}, {viaCep.Bairro}, {cityUf}";
            list.Add(withStreet);
        }

        if (!string.IsNullOrWhiteSpace(viaCep.Bairro))
        {
            list.Add($"{viaCep.Bairro}, {cityUf}");
        }

        list.Add($"{zipCode}, {cityUf}");
        list.Add(cityUf);
        return list.Distinct();
    }

    private static string? NormalizeZip(string? zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode)) return null;
        var digits = new string(zipCode.Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    private sealed class ViaCepResponse
    {
        [JsonPropertyName("cep")]
        public string? Cep { get; set; }

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

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }
}
