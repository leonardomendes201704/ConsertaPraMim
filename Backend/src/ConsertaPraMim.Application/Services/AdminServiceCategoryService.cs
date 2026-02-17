using System.Globalization;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminServiceCategoryService : IAdminServiceCategoryService
{
    private readonly IServiceCategoryRepository _serviceCategoryRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminServiceCategoryService> _logger;

    public AdminServiceCategoryService(
        IServiceCategoryRepository serviceCategoryRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminServiceCategoryService>? logger = null)
    {
        _serviceCategoryRepository = serviceCategoryRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminServiceCategoryService>.Instance;
    }

    public async Task<IReadOnlyList<AdminServiceCategoryDto>> GetAllAsync(bool includeInactive = true)
    {
        var categories = await _serviceCategoryRepository.GetAllAsync(includeInactive);
        return categories
            .OrderBy(c => c.Name)
            .Select(MapDto)
            .ToList();
    }

    public async Task<AdminServiceCategoryUpsertResultDto> CreateAsync(
        AdminCreateServiceCategoryRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!TryNormalizeRequest(
                request.Name,
                request.Slug,
                request.LegacyCategory,
                request.Icon,
                out var normalizedName,
                out var normalizedSlug,
                out var legacyCategory,
                out var normalizedIcon,
                out var validationError))
        {
            return new AdminServiceCategoryUpsertResultDto(false, null, "validation_error", validationError);
        }

        var existingByName = await _serviceCategoryRepository.GetByNameAsync(normalizedName);
        if (existingByName != null)
        {
            return new AdminServiceCategoryUpsertResultDto(false, null, "duplicate_name", "Ja existe uma categoria com esse nome.");
        }

        var existingBySlug = await _serviceCategoryRepository.GetBySlugAsync(normalizedSlug);
        if (existingBySlug != null)
        {
            return new AdminServiceCategoryUpsertResultDto(false, null, "duplicate_slug", "Ja existe uma categoria com esse slug.");
        }

        var category = new ServiceCategoryDefinition
        {
            Name = normalizedName,
            Slug = normalizedSlug,
            Icon = normalizedIcon,
            LegacyCategory = legacyCategory,
            IsActive = true
        };

        await _serviceCategoryRepository.AddAsync(category);
        await WriteAuditAsync(actorUserId, actorEmail, "ServiceCategoryCreated", category.Id, new
        {
            after = new
            {
                category.Name,
                category.Slug,
                category.Icon,
                legacyCategory = category.LegacyCategory.ToString(),
                category.IsActive
            }
        });

        _logger.LogInformation(
            "Admin category created. ActorUserId={ActorUserId}, CategoryId={CategoryId}, Name={CategoryName}",
            actorUserId,
            category.Id,
            category.Name);

        return new AdminServiceCategoryUpsertResultDto(true, MapDto(category));
    }

    public async Task<AdminServiceCategoryUpsertResultDto> UpdateAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var category = await _serviceCategoryRepository.GetByIdAsync(categoryId);
        if (category == null)
        {
            return new AdminServiceCategoryUpsertResultDto(false, null, "not_found", "Categoria nao encontrada.");
        }

        if (!TryNormalizeRequest(
                request.Name,
                request.Slug,
                request.LegacyCategory,
                request.Icon,
                out var normalizedName,
                out var normalizedSlug,
                out var legacyCategory,
                out var normalizedIcon,
                out var validationError))
        {
            return new AdminServiceCategoryUpsertResultDto(false, null, "validation_error", validationError);
        }

        var existingByName = await _serviceCategoryRepository.GetByNameAsync(normalizedName);
        if (existingByName != null && existingByName.Id != category.Id)
        {
            return new AdminServiceCategoryUpsertResultDto(false, null, "duplicate_name", "Ja existe uma categoria com esse nome.");
        }

        var existingBySlug = await _serviceCategoryRepository.GetBySlugAsync(normalizedSlug);
        if (existingBySlug != null && existingBySlug.Id != category.Id)
        {
            return new AdminServiceCategoryUpsertResultDto(false, null, "duplicate_slug", "Ja existe uma categoria com esse slug.");
        }

        var before = new
        {
            category.Name,
            category.Slug,
            category.Icon,
            legacyCategory = category.LegacyCategory.ToString(),
            category.IsActive
        };

        category.Name = normalizedName;
        category.Slug = normalizedSlug;
        category.Icon = normalizedIcon;
        category.LegacyCategory = legacyCategory;
        category.UpdatedAt = DateTime.UtcNow;
        await _serviceCategoryRepository.UpdateAsync(category);

        await WriteAuditAsync(actorUserId, actorEmail, "ServiceCategoryUpdated", category.Id, new
        {
            before,
            after = new
            {
                category.Name,
                category.Slug,
                category.Icon,
                legacyCategory = category.LegacyCategory.ToString(),
                category.IsActive
            }
        });

        _logger.LogInformation(
            "Admin category updated. ActorUserId={ActorUserId}, CategoryId={CategoryId}, Name={CategoryName}",
            actorUserId,
            category.Id,
            category.Name);

        return new AdminServiceCategoryUpsertResultDto(true, MapDto(category));
    }

    public async Task<AdminOperationResultDto> UpdateStatusAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryStatusRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var category = await _serviceCategoryRepository.GetByIdAsync(categoryId);
        if (category == null)
        {
            return new AdminOperationResultDto(false, "not_found", "Categoria nao encontrada.");
        }

        if (category.IsActive == request.IsActive)
        {
            return new AdminOperationResultDto(true);
        }

        if (!request.IsActive)
        {
            var activeCount = (await _serviceCategoryRepository.GetActiveAsync()).Count;
            if (activeCount <= 1)
            {
                return new AdminOperationResultDto(false, "last_active_forbidden", "Nao e permitido inativar a ultima categoria ativa.");
            }
        }

        var before = new { category.IsActive };

        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        await _serviceCategoryRepository.UpdateAsync(category);

        await WriteAuditAsync(actorUserId, actorEmail, "ServiceCategoryStatusChanged", category.Id, new
        {
            before,
            after = new { category.IsActive },
            reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim()
        });

        _logger.LogInformation(
            "Admin category status updated. ActorUserId={ActorUserId}, CategoryId={CategoryId}, IsActive={IsActive}",
            actorUserId,
            category.Id,
            category.IsActive);

        return new AdminOperationResultDto(true);
    }

    private async Task WriteAuditAsync(Guid actorUserId, string actorEmail, string action, Guid targetId, object metadata)
    {
        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = action,
            TargetType = "ServiceCategory",
            TargetId = targetId,
            Metadata = JsonSerializer.Serialize(metadata)
        });
    }

    private static bool TryNormalizeRequest(
        string? name,
        string? slug,
        string? legacyCategoryRaw,
        string? iconRaw,
        out string normalizedName,
        out string normalizedSlug,
        out ServiceCategory legacyCategory,
        out string normalizedIcon,
        out string? validationError)
    {
        normalizedName = (name ?? string.Empty).Trim();
        normalizedSlug = string.Empty;
        normalizedIcon = string.Empty;
        legacyCategory = default;
        validationError = null;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            validationError = "Nome da categoria e obrigatorio.";
            return false;
        }

        if (normalizedName.Length > 100)
        {
            validationError = "Nome da categoria deve ter no maximo 100 caracteres.";
            return false;
        }

        normalizedSlug = NormalizeSlug(slug, normalizedName);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            validationError = "Slug da categoria invalido.";
            return false;
        }

        if (normalizedSlug.Length > 120)
        {
            validationError = "Slug da categoria deve ter no maximo 120 caracteres.";
            return false;
        }

        if (!TryNormalizeIcon(iconRaw, out normalizedIcon, out validationError))
        {
            return false;
        }

        if (!ServiceCategoryExtensions.TryParseFlexible(legacyCategoryRaw, out legacyCategory))
        {
            validationError = "Categoria legada invalida.";
            return false;
        }

        return true;
    }

    private static string NormalizeSlug(string? slug, string fallbackName)
    {
        var value = string.IsNullOrWhiteSpace(slug) ? fallbackName : slug.Trim();
        var normalized = value.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(normalized.Length);
        var previousWasDash = false;

        foreach (var ch in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(lower))
            {
                buffer.Append(lower);
                previousWasDash = false;
            }
            else
            {
                if (previousWasDash || buffer.Length == 0)
                {
                    continue;
                }

                buffer.Append('-');
                previousWasDash = true;
            }
        }

        return buffer.ToString().Trim('-');
    }

    private static bool TryNormalizeIcon(string? iconRaw, out string normalizedIcon, out string? validationError)
    {
        normalizedIcon = (iconRaw ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        validationError = null;

        if (string.IsNullOrWhiteSpace(normalizedIcon))
        {
            validationError = "Icone da categoria e obrigatorio.";
            return false;
        }

        if (normalizedIcon.Length > 80)
        {
            validationError = "Icone da categoria deve ter no maximo 80 caracteres.";
            return false;
        }

        foreach (var ch in normalizedIcon)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_')
            {
                continue;
            }

            validationError = "Icone invalido. Use apenas letras minusculas, numeros e underscore.";
            return false;
        }

        return true;
    }

    private static AdminServiceCategoryDto MapDto(ServiceCategoryDefinition category)
    {
        return new AdminServiceCategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.LegacyCategory.ToString(),
            string.IsNullOrWhiteSpace(category.Icon) ? "build_circle" : category.Icon,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt);
    }
}
