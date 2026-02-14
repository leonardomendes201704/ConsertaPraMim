using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class ServiceAppointmentChecklistService : IServiceAppointmentChecklistService
{
    private const int NoteMaxLength = 1000;

    private readonly IServiceChecklistRepository _serviceChecklistRepository;
    private readonly IServiceAppointmentRepository _serviceAppointmentRepository;
    private readonly INotificationService _notificationService;

    public ServiceAppointmentChecklistService(
        IServiceChecklistRepository serviceChecklistRepository,
        IServiceAppointmentRepository serviceAppointmentRepository,
        INotificationService notificationService)
    {
        _serviceChecklistRepository = serviceChecklistRepository;
        _serviceAppointmentRepository = serviceAppointmentRepository;
        _notificationService = notificationService;
    }

    public async Task<ServiceAppointmentChecklistResultDto> GetChecklistAsync(Guid actorUserId, string actorRole, Guid appointmentId)
    {
        if (!IsSupportedActorRole(actorRole))
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para consultar checklist.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!CanAccessAppointment(appointment, actorUserId, actorRole))
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Usuario sem acesso ao checklist deste agendamento.");
        }

        var template = await ResolveTemplateAsync(appointment, onlyActive: true);
        if (template == null)
        {
            var emptyChecklist = new ServiceAppointmentChecklistDto(
                appointment.Id,
                null,
                null,
                ResolveCategoryName(appointment),
                false,
                0,
                0,
                Array.Empty<ServiceChecklistItemDto>(),
                Array.Empty<ServiceChecklistHistoryDto>());

            return new ServiceAppointmentChecklistResultDto(true, emptyChecklist);
        }

        var activeItems = template.Items
            .Where(i => i.IsActive)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Title)
            .ToList();

        await EnsureResponsesForItemsAsync(appointment.Id, activeItems);
        var responses = await _serviceChecklistRepository.GetResponsesByAppointmentAsync(appointment.Id);
        var responseByItemId = responses.ToDictionary(r => r.TemplateItemId);

        var history = await _serviceChecklistRepository.GetHistoryByAppointmentAsync(appointment.Id);
        var historyDtos = history
            .Select(h => new ServiceChecklistHistoryDto(
                h.Id,
                h.TemplateItemId,
                h.TemplateItem.Title,
                h.PreviousIsChecked,
                h.NewIsChecked,
                h.PreviousNote,
                h.NewNote,
                h.PreviousEvidenceUrl,
                h.NewEvidenceUrl,
                h.ActorUserId,
                h.ActorRole.ToString(),
                h.OccurredAtUtc))
            .ToList();

        var itemDtos = activeItems
            .Select(item =>
            {
                responseByItemId.TryGetValue(item.Id, out var response);
                return new ServiceChecklistItemDto(
                    item.Id,
                    item.Title,
                    item.HelpText,
                    item.IsRequired,
                    item.RequiresEvidence,
                    item.AllowNote,
                    item.SortOrder,
                    response?.IsChecked == true,
                    response?.Note,
                    response?.EvidenceUrl,
                    response?.EvidenceFileName,
                    response?.EvidenceContentType,
                    response?.EvidenceSizeBytes,
                    response?.CheckedByUserId,
                    response?.CheckedAtUtc);
            })
            .ToList();

        var requiredItemsCount = itemDtos.Count(i => i.IsRequired);
        var requiredCompletedCount = itemDtos.Count(i => i.IsRequired && i.IsChecked && (!i.RequiresEvidence || !string.IsNullOrWhiteSpace(i.EvidenceUrl)));

        var checklist = new ServiceAppointmentChecklistDto(
            appointment.Id,
            template.Id,
            template.Name,
            ResolveCategoryName(appointment),
            true,
            requiredItemsCount,
            requiredCompletedCount,
            itemDtos,
            historyDtos);

        return new ServiceAppointmentChecklistResultDto(true, checklist);
    }

    public async Task<ServiceAppointmentChecklistResultDto> UpsertItemResponseAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        UpsertServiceChecklistItemResponseRequestDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Somente prestador/admin pode preencher checklist.");
        }

        if (request.TemplateItemId == Guid.Empty)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "invalid_item", ErrorMessage: "Item de checklist invalido.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador nao pode editar checklist de outro prestador.");
        }

        if (appointment.Status is ServiceAppointmentStatus.CancelledByClient or
            ServiceAppointmentStatus.CancelledByProvider or
            ServiceAppointmentStatus.RejectedByProvider or
            ServiceAppointmentStatus.ExpiredWithoutProviderAction)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "invalid_state", ErrorMessage: "Checklist indisponivel para agendamento encerrado.");
        }

        var template = await ResolveTemplateAsync(appointment, onlyActive: true);
        if (template == null)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "checklist_not_configured", ErrorMessage: "Categoria sem template de checklist ativo.");
        }

        var templateItem = template.Items.FirstOrDefault(i => i.Id == request.TemplateItemId && i.IsActive);
        if (templateItem == null)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "item_not_found", ErrorMessage: "Item de checklist nao encontrado no template ativo.");
        }

        var normalizedNote = NormalizeNote(request.Note, templateItem.AllowNote);
        if (normalizedNote?.Length > NoteMaxLength)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "invalid_note", ErrorMessage: $"Observacao deve ter no maximo {NoteMaxLength} caracteres.");
        }

        var normalizedEvidence = NormalizeEvidence(
            request.EvidenceUrl,
            request.EvidenceFileName,
            request.EvidenceContentType,
            request.EvidenceSizeBytes);
        if (!string.IsNullOrWhiteSpace(request.EvidenceUrl) && normalizedEvidence == null)
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "invalid_evidence", ErrorMessage: "Evidencia invalida para item do checklist.");
        }

        var response = await _serviceChecklistRepository.GetResponseByAppointmentAndItemAsync(appointmentId, templateItem.Id);
        if (response == null)
        {
            response = new ServiceAppointmentChecklistResponse
            {
                ServiceAppointmentId = appointmentId,
                TemplateItemId = templateItem.Id
            };
            await _serviceChecklistRepository.AddResponseAsync(response);
        }

        var previousChecked = response.IsChecked;
        var previousNote = response.Note;
        var previousEvidenceUrl = response.EvidenceUrl;

        if (request.ClearEvidence)
        {
            response.EvidenceUrl = null;
            response.EvidenceFileName = null;
            response.EvidenceContentType = null;
            response.EvidenceSizeBytes = null;
        }
        else if (normalizedEvidence != null)
        {
            response.EvidenceUrl = normalizedEvidence.Url;
            response.EvidenceFileName = normalizedEvidence.FileName;
            response.EvidenceContentType = normalizedEvidence.ContentType;
            response.EvidenceSizeBytes = normalizedEvidence.SizeBytes;
        }

        response.Note = normalizedNote;
        response.IsChecked = request.IsChecked;
        response.CheckedByUserId = actorUserId;
        response.CheckedAtUtc = DateTime.UtcNow;
        response.UpdatedAt = DateTime.UtcNow;

        if (templateItem.RequiresEvidence &&
            response.IsChecked &&
            string.IsNullOrWhiteSpace(response.EvidenceUrl))
        {
            return new ServiceAppointmentChecklistResultDto(false, ErrorCode: "evidence_required", ErrorMessage: "Esse item exige evidencia (foto/video) para ser concluido.");
        }

        await _serviceChecklistRepository.UpdateResponseAsync(response);

        if (previousChecked != response.IsChecked ||
            !string.Equals(previousNote, response.Note, StringComparison.Ordinal) ||
            !string.Equals(previousEvidenceUrl, response.EvidenceUrl, StringComparison.Ordinal))
        {
            await _serviceChecklistRepository.AddHistoryAsync(new ServiceAppointmentChecklistHistory
            {
                ServiceAppointmentId = appointmentId,
                TemplateItemId = templateItem.Id,
                PreviousIsChecked = previousChecked,
                NewIsChecked = response.IsChecked,
                PreviousNote = previousNote,
                NewNote = response.Note,
                PreviousEvidenceUrl = previousEvidenceUrl,
                NewEvidenceUrl = response.EvidenceUrl,
                ActorUserId = actorUserId,
                ActorRole = ResolveActorRole(actorRole),
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        await _notificationService.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            "Agendamento: checklist tecnico atualizado",
            $"Checklist tecnico atualizado para o atendimento de {ResolveCategoryName(appointment)}.",
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            appointment.ProviderId.ToString("N"),
            "Agendamento: checklist tecnico atualizado",
            $"Checklist tecnico atualizado para o atendimento de {ResolveCategoryName(appointment)}.",
            BuildActionUrl(appointment.ServiceRequestId));

        return await GetChecklistAsync(actorUserId, actorRole, appointmentId);
    }

    public async Task<ServiceAppointmentChecklistValidationResultDto> ValidateRequiredItemsForCompletionAsync(
        Guid appointmentId,
        string? actorRole = null)
    {
        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentChecklistValidationResultDto(
                Success: false,
                CanComplete: false,
                PendingRequiredCount: 0,
                PendingRequiredItems: Array.Empty<string>(),
                ErrorCode: "appointment_not_found",
                ErrorMessage: "Agendamento nao encontrado.");
        }

        var template = await ResolveTemplateAsync(appointment, onlyActive: true);
        if (template == null)
        {
            return new ServiceAppointmentChecklistValidationResultDto(
                Success: true,
                CanComplete: true,
                PendingRequiredCount: 0,
                PendingRequiredItems: Array.Empty<string>());
        }

        var requiredItems = template.Items
            .Where(i => i.IsActive && i.IsRequired)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Title)
            .ToList();

        if (requiredItems.Count == 0)
        {
            return new ServiceAppointmentChecklistValidationResultDto(
                Success: true,
                CanComplete: true,
                PendingRequiredCount: 0,
                PendingRequiredItems: Array.Empty<string>());
        }

        var responses = await _serviceChecklistRepository.GetResponsesByAppointmentAsync(appointmentId);
        var responseByItem = responses.ToDictionary(r => r.TemplateItemId);

        var pendingItems = requiredItems
            .Where(item =>
            {
                if (!responseByItem.TryGetValue(item.Id, out var response))
                {
                    return true;
                }

                if (!response.IsChecked)
                {
                    return true;
                }

                return item.RequiresEvidence && string.IsNullOrWhiteSpace(response.EvidenceUrl);
            })
            .Select(i => i.Title)
            .ToList();

        return new ServiceAppointmentChecklistValidationResultDto(
            Success: true,
            CanComplete: pendingItems.Count == 0,
            PendingRequiredCount: pendingItems.Count,
            PendingRequiredItems: pendingItems,
            ErrorCode: pendingItems.Count == 0 ? null : "required_items_pending",
            ErrorMessage: pendingItems.Count == 0
                ? null
                : $"Checklist incompleto. Itens pendentes: {string.Join(", ", pendingItems)}");
    }

    private async Task EnsureResponsesForItemsAsync(Guid appointmentId, IReadOnlyCollection<ServiceChecklistTemplateItem> activeItems)
    {
        if (activeItems.Count == 0)
        {
            return;
        }

        var existing = await _serviceChecklistRepository.GetResponsesByAppointmentAsync(appointmentId);
        var existingItemIds = existing
            .Select(r => r.TemplateItemId)
            .ToHashSet();

        foreach (var item in activeItems.Where(item => !existingItemIds.Contains(item.Id)))
        {
            await _serviceChecklistRepository.AddResponseAsync(new ServiceAppointmentChecklistResponse
            {
                ServiceAppointmentId = appointmentId,
                TemplateItemId = item.Id,
                IsChecked = false
            });
        }
    }

    private async Task<ServiceChecklistTemplate?> ResolveTemplateAsync(ServiceAppointment appointment, bool onlyActive)
    {
        if (appointment.ServiceRequest.CategoryDefinitionId.HasValue)
        {
            var byDefinition = await _serviceChecklistRepository.GetTemplateByCategoryDefinitionAsync(
                appointment.ServiceRequest.CategoryDefinitionId.Value,
                onlyActive);
            if (byDefinition != null)
            {
                return byDefinition;
            }
        }

        return await _serviceChecklistRepository.GetTemplateByLegacyCategoryAsync(
            appointment.ServiceRequest.Category,
            onlyActive);
    }

    private static string? NormalizeNote(string? rawNote, bool allowNote)
    {
        if (!allowNote)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(rawNote) ? null : rawNote.Trim();
    }

    private static ChecklistEvidenceSnapshot? NormalizeEvidence(
        string? evidenceUrl,
        string? evidenceFileName,
        string? evidenceContentType,
        long? evidenceSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(evidenceUrl))
        {
            return null;
        }

        var trimmed = evidenceUrl.Trim();
        var isRelativeUpload =
            trimmed.StartsWith("/uploads/service-checklists/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/uploads/provider-gallery/", StringComparison.OrdinalIgnoreCase);

        if (!isRelativeUpload)
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var path = uri.AbsolutePath;
            if (!path.StartsWith("/uploads/service-checklists/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/uploads/provider-gallery/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return new ChecklistEvidenceSnapshot(
            trimmed,
            string.IsNullOrWhiteSpace(evidenceFileName) ? null : evidenceFileName.Trim(),
            string.IsNullOrWhiteSpace(evidenceContentType) ? null : evidenceContentType.Trim(),
            evidenceSizeBytes.HasValue && evidenceSizeBytes.Value > 0 ? evidenceSizeBytes : null);
    }

    private static bool IsSupportedActorRole(string actorRole)
    {
        return IsClientRole(actorRole) || IsProviderRole(actorRole) || IsAdminRole(actorRole);
    }

    private static bool CanAccessAppointment(ServiceAppointment appointment, Guid actorUserId, string actorRole)
    {
        if (IsAdminRole(actorRole))
        {
            return true;
        }

        if (IsClientRole(actorRole))
        {
            return appointment.ClientId == actorUserId;
        }

        if (IsProviderRole(actorRole))
        {
            return appointment.ProviderId == actorUserId;
        }

        return false;
    }

    private static bool IsClientRole(string role)
    {
        return role.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProviderRole(string role)
    {
        return role.Equals(UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminRole(string role)
    {
        return role.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceAppointmentActorRole ResolveActorRole(string role)
    {
        if (IsAdminRole(role))
        {
            return ServiceAppointmentActorRole.Admin;
        }

        if (IsClientRole(role))
        {
            return ServiceAppointmentActorRole.Client;
        }

        if (IsProviderRole(role))
        {
            return ServiceAppointmentActorRole.Provider;
        }

        return ServiceAppointmentActorRole.System;
    }

    private static string ResolveCategoryName(ServiceAppointment appointment)
    {
        if (!string.IsNullOrWhiteSpace(appointment.ServiceRequest.CategoryDefinition?.Name))
        {
            return appointment.ServiceRequest.CategoryDefinition.Name;
        }

        return appointment.ServiceRequest.Category.ToPtBr();
    }

    private static string BuildActionUrl(Guid requestId)
    {
        return $"/ServiceRequests/Details/{requestId}";
    }

    private sealed record ChecklistEvidenceSnapshot(
        string Url,
        string? FileName,
        string? ContentType,
        long? SizeBytes);
}
