using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminChecklistTemplateService : IAdminChecklistTemplateService
{
    private const int TemplateNameMaxLength = 120;
    private const int TemplateDescriptionMaxLength = 500;
    private const int ItemTitleMaxLength = 180;
    private const int ItemHelpTextMaxLength = 500;

    private readonly IServiceChecklistRepository _serviceChecklistRepository;
    private readonly IServiceCategoryRepository _serviceCategoryRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminChecklistTemplateService> _logger;

    public AdminChecklistTemplateService(
        IServiceChecklistRepository serviceChecklistRepository,
        IServiceCategoryRepository serviceCategoryRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminChecklistTemplateService>? logger = null)
    {
        _serviceChecklistRepository = serviceChecklistRepository;
        _serviceCategoryRepository = serviceCategoryRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminChecklistTemplateService>.Instance;
    }

    public async Task<IReadOnlyList<AdminChecklistTemplateDto>> GetAllAsync(bool includeInactive = true)
    {
        var templates = await _serviceChecklistRepository.GetTemplatesAsync(includeInactive);
        return templates
            .OrderBy(t => t.CategoryDefinition.Name)
            .ThenBy(t => t.Name)
            .Select(MapTemplateDto)
            .ToList();
    }

    public async Task<AdminChecklistTemplateUpsertResultDto> CreateAsync(
        AdminCreateChecklistTemplateRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var categoryDefinition = await _serviceCategoryRepository.GetByIdAsync(request.CategoryDefinitionId);
        if (categoryDefinition == null)
        {
            return new AdminChecklistTemplateUpsertResultDto(false, ErrorCode: "category_not_found", ErrorMessage: "Categoria nao encontrada.");
        }

        if (!categoryDefinition.IsActive)
        {
            return new AdminChecklistTemplateUpsertResultDto(false, ErrorCode: "inactive_category", ErrorMessage: "Apenas categorias ativas podem receber templates.");
        }

        var existing = await _serviceChecklistRepository.GetTemplateByCategoryDefinitionAsync(request.CategoryDefinitionId, onlyActive: false);
        if (existing != null)
        {
            return new AdminChecklistTemplateUpsertResultDto(false, ErrorCode: "template_already_exists", ErrorMessage: "Ja existe template cadastrado para essa categoria.");
        }

        if (!TryNormalizeTemplateRequest(request.Name, request.Description, request.Items, out var normalizedName, out var normalizedDescription, out var normalizedItems, out var errorMessage))
        {
            return new AdminChecklistTemplateUpsertResultDto(false, ErrorCode: "validation_error", ErrorMessage: errorMessage);
        }

        var template = new ServiceChecklistTemplate
        {
            CategoryDefinitionId = categoryDefinition.Id,
            Name = normalizedName,
            Description = normalizedDescription,
            IsActive = true,
            Items = normalizedItems
                .Select(item => new ServiceChecklistTemplateItem
                {
                    Title = item.Title,
                    HelpText = item.HelpText,
                    IsRequired = item.IsRequired,
                    RequiresEvidence = item.RequiresEvidence,
                    AllowNote = item.AllowNote,
                    IsActive = item.IsActive,
                    SortOrder = item.SortOrder
                })
                .ToList()
        };

        await _serviceChecklistRepository.AddTemplateAsync(template);

        var created = await _serviceChecklistRepository.GetTemplateByIdAsync(template.Id) ?? template;
        await WriteAuditAsync(actorUserId, actorEmail, "ServiceChecklistTemplateCreated", template.Id, new
        {
            categoryDefinitionId = categoryDefinition.Id,
            categoryDefinitionName = categoryDefinition.Name,
            created.Name,
            created.Description,
            itemCount = created.Items.Count
        });

        _logger.LogInformation(
            "Checklist template created. ActorUserId={ActorUserId}, TemplateId={TemplateId}, CategoryDefinitionId={CategoryDefinitionId}",
            actorUserId,
            created.Id,
            created.CategoryDefinitionId);

        return new AdminChecklistTemplateUpsertResultDto(true, MapTemplateDto(created));
    }

    public async Task<AdminChecklistTemplateUpsertResultDto> UpdateAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var template = await _serviceChecklistRepository.GetTemplateByIdAsync(templateId);
        if (template == null)
        {
            return new AdminChecklistTemplateUpsertResultDto(false, ErrorCode: "template_not_found", ErrorMessage: "Template nao encontrado.");
        }

        if (!TryNormalizeTemplateRequest(request.Name, request.Description, request.Items, out var normalizedName, out var normalizedDescription, out var normalizedItems, out var errorMessage))
        {
            return new AdminChecklistTemplateUpsertResultDto(false, ErrorCode: "validation_error", ErrorMessage: errorMessage);
        }

        var before = new
        {
            template.Name,
            template.Description,
            template.IsActive,
            items = template.Items.Select(item => new
            {
                item.Id,
                item.Title,
                item.HelpText,
                item.IsRequired,
                item.RequiresEvidence,
                item.AllowNote,
                item.IsActive,
                item.SortOrder
            }).ToList()
        };

        template.Name = normalizedName;
        template.Description = normalizedDescription;
        template.UpdatedAt = DateTime.UtcNow;

        var nowUtc = DateTime.UtcNow;
        var existingById = template.Items.ToDictionary(i => i.Id);
        var touchedIds = new HashSet<Guid>();

        foreach (var normalizedItem in normalizedItems)
        {
            if (normalizedItem.Id.HasValue &&
                normalizedItem.Id.Value != Guid.Empty &&
                existingById.TryGetValue(normalizedItem.Id.Value, out var existingItem))
            {
                existingItem.Title = normalizedItem.Title;
                existingItem.HelpText = normalizedItem.HelpText;
                existingItem.IsRequired = normalizedItem.IsRequired;
                existingItem.RequiresEvidence = normalizedItem.RequiresEvidence;
                existingItem.AllowNote = normalizedItem.AllowNote;
                existingItem.IsActive = normalizedItem.IsActive;
                existingItem.SortOrder = normalizedItem.SortOrder;
                existingItem.UpdatedAt = nowUtc;
                touchedIds.Add(existingItem.Id);
                continue;
            }

            var newItem = new ServiceChecklistTemplateItem
            {
                TemplateId = template.Id,
                Title = normalizedItem.Title,
                HelpText = normalizedItem.HelpText,
                IsRequired = normalizedItem.IsRequired,
                RequiresEvidence = normalizedItem.RequiresEvidence,
                AllowNote = normalizedItem.AllowNote,
                IsActive = normalizedItem.IsActive,
                SortOrder = normalizedItem.SortOrder
            };
            template.Items.Add(newItem);
            touchedIds.Add(newItem.Id);
        }

        foreach (var staleItem in template.Items.Where(i => !touchedIds.Contains(i.Id)).ToList())
        {
            staleItem.IsActive = false;
            staleItem.UpdatedAt = nowUtc;
        }

        await _serviceChecklistRepository.UpdateTemplateAsync(template);

        var updated = await _serviceChecklistRepository.GetTemplateByIdAsync(template.Id) ?? template;
        await WriteAuditAsync(actorUserId, actorEmail, "ServiceChecklistTemplateUpdated", template.Id, new
        {
            before,
            after = new
            {
                updated.Name,
                updated.Description,
                updated.IsActive,
                items = updated.Items.Select(item => new
                {
                    item.Id,
                    item.Title,
                    item.HelpText,
                    item.IsRequired,
                    item.RequiresEvidence,
                    item.AllowNote,
                    item.IsActive,
                    item.SortOrder
                }).ToList()
            }
        });

        _logger.LogInformation(
            "Checklist template updated. ActorUserId={ActorUserId}, TemplateId={TemplateId}, ItemCount={ItemCount}",
            actorUserId,
            updated.Id,
            updated.Items.Count);

        return new AdminChecklistTemplateUpsertResultDto(true, MapTemplateDto(updated));
    }

    public async Task<AdminOperationResultDto> UpdateStatusAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateStatusRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var template = await _serviceChecklistRepository.GetTemplateByIdAsync(templateId);
        if (template == null)
        {
            return new AdminOperationResultDto(false, "template_not_found", "Template nao encontrado.");
        }

        if (template.IsActive == request.IsActive)
        {
            return new AdminOperationResultDto(true);
        }

        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        await _serviceChecklistRepository.UpdateTemplateAsync(template);

        await WriteAuditAsync(actorUserId, actorEmail, "ServiceChecklistTemplateStatusChanged", template.Id, new
        {
            isActive = template.IsActive,
            reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim()
        });

        _logger.LogInformation(
            "Checklist template status updated. ActorUserId={ActorUserId}, TemplateId={TemplateId}, IsActive={IsActive}",
            actorUserId,
            template.Id,
            template.IsActive);

        return new AdminOperationResultDto(true);
    }

    private async Task WriteAuditAsync(Guid actorUserId, string actorEmail, string action, Guid targetId, object metadata)
    {
        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = action,
            TargetType = "ServiceChecklistTemplate",
            TargetId = targetId,
            Metadata = JsonSerializer.Serialize(metadata)
        });
    }

    private static AdminChecklistTemplateDto MapTemplateDto(ServiceChecklistTemplate template)
    {
        return new AdminChecklistTemplateDto(
            template.Id,
            template.CategoryDefinitionId,
            template.CategoryDefinition.Name,
            template.CategoryDefinition.LegacyCategory.ToString(),
            template.Name,
            template.Description,
            template.IsActive,
            template.CreatedAt,
            template.UpdatedAt,
            template.Items
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Title)
                .Select(MapItemDto)
                .ToList());
    }

    private static AdminChecklistTemplateItemDto MapItemDto(ServiceChecklistTemplateItem item)
    {
        return new AdminChecklistTemplateItemDto(
            item.Id,
            item.Title,
            item.HelpText,
            item.IsRequired,
            item.RequiresEvidence,
            item.AllowNote,
            item.IsActive,
            item.SortOrder,
            item.CreatedAt,
            item.UpdatedAt);
    }

    private static bool TryNormalizeTemplateRequest(
        string? name,
        string? description,
        IReadOnlyList<AdminChecklistTemplateItemUpsertDto>? items,
        out string normalizedName,
        out string? normalizedDescription,
        out IReadOnlyList<AdminChecklistTemplateItemUpsertDto> normalizedItems,
        out string? errorMessage)
    {
        normalizedName = (name ?? string.Empty).Trim();
        normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        normalizedItems = Array.Empty<AdminChecklistTemplateItemUpsertDto>();
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            errorMessage = "Nome do template e obrigatorio.";
            return false;
        }

        if (normalizedName.Length > TemplateNameMaxLength)
        {
            errorMessage = $"Nome do template deve ter no maximo {TemplateNameMaxLength} caracteres.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedDescription) &&
            normalizedDescription.Length > TemplateDescriptionMaxLength)
        {
            errorMessage = $"Descricao do template deve ter no maximo {TemplateDescriptionMaxLength} caracteres.";
            return false;
        }

        if (items == null || items.Count == 0)
        {
            errorMessage = "Informe ao menos um item de checklist.";
            return false;
        }

        var normalized = new List<AdminChecklistTemplateItemUpsertDto>(items.Count);
        var hasRequiredItem = false;

        foreach (var item in items)
        {
            var title = (item.Title ?? string.Empty).Trim();
            var helpText = string.IsNullOrWhiteSpace(item.HelpText) ? null : item.HelpText.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                errorMessage = "Todo item deve ter titulo.";
                return false;
            }

            if (title.Length > ItemTitleMaxLength)
            {
                errorMessage = $"Titulo de item deve ter no maximo {ItemTitleMaxLength} caracteres.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(helpText) && helpText.Length > ItemHelpTextMaxLength)
            {
                errorMessage = $"Texto de apoio do item deve ter no maximo {ItemHelpTextMaxLength} caracteres.";
                return false;
            }

            if (item.SortOrder < 0 || item.SortOrder > 10_000)
            {
                errorMessage = "SortOrder invalido para item do checklist.";
                return false;
            }

            hasRequiredItem |= item.IsRequired && item.IsActive;

            normalized.Add(new AdminChecklistTemplateItemUpsertDto(
                item.Id,
                title,
                helpText,
                item.IsRequired,
                item.RequiresEvidence,
                item.AllowNote,
                item.IsActive,
                item.SortOrder));
        }

        if (!hasRequiredItem)
        {
            errorMessage = "O template precisa ter ao menos um item obrigatorio ativo.";
            return false;
        }

        normalizedItems = normalized
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Title)
            .ToList();
        return true;
    }
}
