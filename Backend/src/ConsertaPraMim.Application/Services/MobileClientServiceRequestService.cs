using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public class MobileClientServiceRequestService : IMobileClientServiceRequestService
{
    private readonly IServiceCategoryCatalogService _serviceCategoryCatalogService;
    private readonly IZipGeocodingService _zipGeocodingService;
    private readonly IServiceRequestService _serviceRequestService;

    public MobileClientServiceRequestService(
        IServiceCategoryCatalogService serviceCategoryCatalogService,
        IZipGeocodingService zipGeocodingService,
        IServiceRequestService serviceRequestService)
    {
        _serviceCategoryCatalogService = serviceCategoryCatalogService;
        _zipGeocodingService = zipGeocodingService;
        _serviceRequestService = serviceRequestService;
    }

    public async Task<IReadOnlyList<MobileClientServiceRequestCategoryDto>> GetActiveCategoriesAsync()
    {
        var categories = await _serviceCategoryCatalogService.GetActiveAsync();
        return categories
            .Select(category => new MobileClientServiceRequestCategoryDto(
                category.Id,
                category.Name,
                category.Slug,
                category.LegacyCategory,
                ResolveCategoryIcon(category.Name, category.LegacyCategory)))
            .ToList();
    }

    public async Task<MobileClientResolveZipResponseDto?> ResolveZipAsync(string zipCode)
    {
        var normalizedZip = NormalizeZip(zipCode);
        if (string.IsNullOrWhiteSpace(normalizedZip) || normalizedZip.Length != 8)
        {
            return null;
        }

        var resolved = await _zipGeocodingService.ResolveCoordinatesAsync(normalizedZip);
        if (!resolved.HasValue)
        {
            return null;
        }

        return new MobileClientResolveZipResponseDto(
            resolved.Value.NormalizedZip,
            string.IsNullOrWhiteSpace(resolved.Value.Street) ? "Endereco nao informado" : resolved.Value.Street,
            string.IsNullOrWhiteSpace(resolved.Value.City) ? "Cidade nao informada" : resolved.Value.City,
            resolved.Value.Latitude,
            resolved.Value.Longitude);
    }

    public async Task<MobileClientCreateServiceRequestResponseDto> CreateAsync(
        Guid clientUserId,
        MobileClientCreateServiceRequestRequestDto request)
    {
        var normalizedDescription = (request.Description ?? string.Empty).Trim();
        if (request.CategoryId == Guid.Empty)
        {
            throw new InvalidOperationException("Selecione uma categoria valida para criar o pedido.");
        }

        if (normalizedDescription.Length < 8)
        {
            throw new InvalidOperationException("Descreva melhor o problema (minimo de 8 caracteres).");
        }

        var normalizedZip = NormalizeZip(request.ZipCode);
        if (normalizedZip.Length != 8)
        {
            throw new InvalidOperationException("Informe um CEP valido com 8 digitos.");
        }

        var createDto = new CreateServiceRequestDto(
            request.CategoryId,
            Category: null,
            Description: normalizedDescription,
            Street: (request.Street ?? string.Empty).Trim(),
            City: (request.City ?? string.Empty).Trim(),
            Zip: normalizedZip,
            Lat: 0,
            Lng: 0);

        var requestId = await _serviceRequestService.CreateAsync(clientUserId, createDto);
        var createdRequest = await _serviceRequestService.GetByIdAsync(
            requestId,
            clientUserId,
            UserRole.Client.ToString());

        if (createdRequest == null)
        {
            throw new InvalidOperationException("Pedido criado, mas nao foi possivel montar o retorno para o app.");
        }

        var order = new MobileClientOrderItemDto(
            createdRequest.Id,
            ResolveOrderTitle(createdRequest.Category, createdRequest.Description),
            NormalizeOrderStatus(createdRequest.Status),
            createdRequest.Category,
            createdRequest.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            ResolveCategoryIcon(createdRequest.Category, createdRequest.Category),
            createdRequest.Description,
            ProposalCount: 0);

        return new MobileClientCreateServiceRequestResponseDto(
            order,
            createdRequest.Street,
            createdRequest.City,
            createdRequest.Zip,
            "Pedido criado com sucesso! Aguarde propostas profissionais.");
    }

    private static string NormalizeZip(string? zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return string.Empty;
        }

        return new string(zipCode.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeOrderStatus(string status)
    {
        var normalized = (status ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is "INPROGRESS" or "IN_PROGRESS" or "EM_ANDAMENTO")
        {
            return "EM_ANDAMENTO";
        }

        if (normalized is "COMPLETED" or "VALIDATED" or "CONCLUIDO")
        {
            return "CONCLUIDO";
        }

        if (normalized is "CANCELED" or "CANCELLED" or "CANCELADO")
        {
            return "CANCELADO";
        }

        return "AGUARDANDO";
    }

    private static string ResolveOrderTitle(string category, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            var compactDescription = description.Trim();
            if (compactDescription.Length <= 48)
            {
                return compactDescription;
            }

            return compactDescription[..45].TrimEnd() + "...";
        }

        return $"Pedido de {category}";
    }

    private static string ResolveCategoryIcon(string categoryName, string legacyCategory)
    {
        var normalized = $"{categoryName} {legacyCategory}".Trim().ToLowerInvariant();

        if (normalized.Contains("eletric"))
        {
            return "bolt";
        }

        if (normalized.Contains("hidraul") || normalized.Contains("encan"))
        {
            return "water_drop";
        }

        if (normalized.Contains("pintur"))
        {
            return "format_paint";
        }

        if (normalized.Contains("montag") || normalized.Contains("marcen"))
        {
            return "construction";
        }

        if (normalized.Contains("limpez"))
        {
            return "cleaning_services";
        }

        if (normalized.Contains("alvenar") || normalized.Contains("pedre"))
        {
            return "foundation";
        }

        if (normalized.Contains("eletron") || normalized.Contains("informat"))
        {
            return "memory";
        }

        if (normalized.Contains("eletrodom"))
        {
            return "kitchen";
        }

        if (normalized.Contains("ar condicionado"))
        {
            return "ac_unit";
        }

        if (normalized.Contains("jardin"))
        {
            return "yard";
        }

        return "build_circle";
    }
}
