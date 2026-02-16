using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class MobileClientOrderService : IMobileClientOrderService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;

    public MobileClientOrderService(IServiceRequestRepository serviceRequestRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
    }

    public async Task<MobileClientOrdersResponseDto> GetMyOrdersAsync(Guid clientUserId, int takePerBucket = 100)
    {
        var normalizedTake = Math.Clamp(takePerBucket, 1, 300);
        var requests = await _serviceRequestRepository.GetByClientIdAsync(clientUserId);

        var projected = requests
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new
            {
                Request = request,
                Item = MapToMobileOrderItem(request)
            })
            .ToList();

        var openOrders = projected
            .Where(item => !IsFinalizedStatus(item.Request.Status))
            .Take(normalizedTake)
            .Select(item => item.Item)
            .ToList();

        var finalizedOrders = projected
            .Where(item => IsFinalizedStatus(item.Request.Status))
            .Take(normalizedTake)
            .Select(item => item.Item)
            .ToList();

        return new MobileClientOrdersResponseDto(
            openOrders,
            finalizedOrders,
            openOrders.Count,
            finalizedOrders.Count,
            openOrders.Count + finalizedOrders.Count);
    }

    private static MobileClientOrderItemDto MapToMobileOrderItem(ServiceRequest request)
    {
        var category = ResolveCategoryDisplay(request);
        var normalizedDescription = request.Description?.Trim();

        return new MobileClientOrderItemDto(
            request.Id,
            ResolveTitle(category, normalizedDescription),
            MapStatusToMobileStatus(request.Status),
            category,
            request.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            ResolveCategoryIcon(category),
            normalizedDescription);
    }

    private static bool IsFinalizedStatus(ServiceRequestStatus status)
    {
        return status == ServiceRequestStatus.Completed ||
               status == ServiceRequestStatus.Validated ||
               status == ServiceRequestStatus.Canceled;
    }

    private static string MapStatusToMobileStatus(ServiceRequestStatus status)
    {
        return status switch
        {
            ServiceRequestStatus.InProgress => "EM_ANDAMENTO",
            ServiceRequestStatus.Completed => "CONCLUIDO",
            ServiceRequestStatus.Validated => "CONCLUIDO",
            ServiceRequestStatus.Canceled => "CANCELADO",
            _ => "AGUARDANDO"
        };
    }

    private static string ResolveTitle(string category, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            var compact = description.Trim();
            if (compact.Length <= 48)
            {
                return compact;
            }

            return compact[..45].TrimEnd() + "...";
        }

        return $"Pedido de {category}";
    }

    private static string ResolveCategoryDisplay(ServiceRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name)
            ? request.CategoryDefinition!.Name
            : request.Category.ToString();
    }

    private static string ResolveCategoryIcon(string categoryName)
    {
        var normalized = categoryName.Trim().ToLowerInvariant();

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

        if (normalized.Contains("alvenar"))
        {
            return "home_repair_service";
        }

        if (normalized.Contains("eletron"))
        {
            return "memory";
        }

        if (normalized.Contains("eletrodom"))
        {
            return "kitchen";
        }

        if (normalized.Contains("jardin"))
        {
            return "yard";
        }

        return "build_circle";
    }
}
