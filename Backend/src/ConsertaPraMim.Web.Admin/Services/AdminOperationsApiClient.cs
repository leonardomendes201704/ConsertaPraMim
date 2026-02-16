using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace ConsertaPraMim.Web.Admin.Services;

public class AdminOperationsApiClient : IAdminOperationsApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminOperationsApiClient> _logger;

    public AdminOperationsApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdminOperationsApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminApiResult<AdminServiceRequestsListResponseDto>> GetServiceRequestsAsync(
        AdminServiceRequestsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminServiceRequestsListResponseDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = BuildServiceRequestsUrl(baseUrl, filters);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminServiceRequestsListResponseDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar pedidos.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminServiceRequestsListResponseDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminServiceRequestsListResponseDto>.Fail("Resposta vazia da API de pedidos.")
            : AdminApiResult<AdminServiceRequestsListResponseDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminServiceRequestDetailsDto>> GetServiceRequestByIdAsync(
        Guid requestId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminServiceRequestDetailsDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/service-requests/{requestId:D}";
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminServiceRequestDetailsDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar detalhes do pedido.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminServiceRequestDetailsDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminServiceRequestDetailsDto>.Fail("Resposta vazia da API de pedidos.")
            : AdminApiResult<AdminServiceRequestDetailsDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdateServiceRequestStatusAsync(
        Guid requestId,
        string status,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/service-requests/{requestId:D}/status";
        var payload = new AdminUpdateServiceRequestStatusRequestDto(status, reason);
        return await SendAdminOperationAsync(url, payload, accessToken, cancellationToken);
    }

    public async Task<AdminApiResult<AdminProposalsListResponseDto>> GetProposalsAsync(
        AdminProposalsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminProposalsListResponseDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = BuildProposalsUrl(baseUrl, filters);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminProposalsListResponseDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar propostas.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminProposalsListResponseDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminProposalsListResponseDto>.Fail("Resposta vazia da API de propostas.")
            : AdminApiResult<AdminProposalsListResponseDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> InvalidateProposalAsync(
        Guid proposalId,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/proposals/{proposalId:D}/invalidate";
        var payload = new AdminInvalidateProposalRequestDto(reason);
        return await SendAdminOperationAsync(url, payload, accessToken, cancellationToken);
    }

    public async Task<AdminApiResult<AdminChatsListResponseDto>> GetChatsAsync(
        AdminChatsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminChatsListResponseDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = BuildChatsUrl(baseUrl, filters);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminChatsListResponseDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar conversas.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminChatsListResponseDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminChatsListResponseDto>.Fail("Resposta vazia da API de conversas.")
            : AdminApiResult<AdminChatsListResponseDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminChatDetailsDto>> GetChatAsync(
        Guid requestId,
        Guid providerId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminChatDetailsDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/chats/{requestId:D}/{providerId:D}";
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminChatDetailsDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar historico da conversa.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminChatDetailsDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminChatDetailsDto>.Fail("Resposta vazia da API de conversas.")
            : AdminApiResult<AdminChatDetailsDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminChatAttachmentsListResponseDto>> GetChatAttachmentsAsync(
        AdminChatAttachmentsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminChatAttachmentsListResponseDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = BuildChatAttachmentsUrl(baseUrl, filters);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminChatAttachmentsListResponseDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar anexos de chat.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminChatAttachmentsListResponseDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminChatAttachmentsListResponseDto>.Fail("Resposta vazia da API de anexos.")
            : AdminApiResult<AdminChatAttachmentsListResponseDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminDisputesQueueResponseDto>> GetDisputesQueueAsync(
        AdminDisputesQueueFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminDisputesQueueResponseDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = BuildDisputesQueueUrl(baseUrl, filters);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminDisputesQueueResponseDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar fila de disputas.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminDisputesQueueResponseDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminDisputesQueueResponseDto>.Fail("Resposta vazia da API de disputas.")
            : AdminApiResult<AdminDisputesQueueResponseDto>.Ok(payload);
    }

    public async Task<AdminApiResult<string>> ExportDisputesQueueCsvAsync(
        AdminDisputesQueueFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<string>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = BuildDisputesQueueExportUrl(baseUrl, filters);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<string>.Fail(
                response.ErrorMessage ?? "Falha ao exportar fila de disputas.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(payload)
            ? AdminApiResult<string>.Fail("Resposta vazia na exportacao de disputas.")
            : AdminApiResult<string>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminDisputeCaseDetailsDto>> GetDisputeByIdAsync(
        Guid disputeCaseId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminDisputeCaseDetailsDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/disputes/{disputeCaseId:D}";
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminDisputeCaseDetailsDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar detalhes da disputa.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminDisputeCaseDetailsDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminDisputeCaseDetailsDto>.Fail("Resposta vazia da API de disputa.")
            : AdminApiResult<AdminDisputeCaseDetailsDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminDisputeOperationResultDto>> UpdateDisputeWorkflowAsync(
        Guid disputeCaseId,
        AdminUpdateDisputeWorkflowRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminDisputeOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/disputes/{disputeCaseId:D}/workflow";
        var response = await SendAsync(HttpMethod.Put, url, accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminDisputeOperationResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao atualizar workflow da disputa.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminDisputeOperationResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminDisputeOperationResultDto>.Fail("Resposta vazia ao atualizar workflow da disputa.")
            : AdminApiResult<AdminDisputeOperationResultDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminDisputeOperationResultDto>> RegisterDisputeDecisionAsync(
        Guid disputeCaseId,
        AdminRegisterDisputeDecisionRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminDisputeOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/disputes/{disputeCaseId:D}/decision";
        var response = await SendAsync(HttpMethod.Post, url, accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminDisputeOperationResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao registrar decisao da disputa.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminDisputeOperationResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminDisputeOperationResultDto>.Fail("Resposta vazia ao registrar decisao da disputa.")
            : AdminApiResult<AdminDisputeOperationResultDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminSendNotificationResultDto>> SendNotificationAsync(
        Guid recipientUserId,
        string subject,
        string message,
        string? actionUrl,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminSendNotificationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var payload = new AdminSendNotificationRequestDto(
            recipientUserId,
            subject,
            message,
            actionUrl,
            reason);

        var url = $"{baseUrl}/api/admin/notifications/send";
        var response = await SendAsync(HttpMethod.Post, url, accessToken, payload, cancellationToken);
        if (!response.Success)
        {
            var errorDto = response.ErrorNotification;
            return AdminApiResult<AdminSendNotificationResultDto>.Fail(
                errorDto?.ErrorMessage ?? response.ErrorMessage ?? "Falha ao enviar notificacao.",
                errorDto?.ErrorCode ?? response.ErrorCode,
                response.StatusCode);
        }

        if (response.HttpResponse == null)
        {
            return AdminApiResult<AdminSendNotificationResultDto>.Fail("Resposta invalida ao enviar notificacao.");
        }

        var result = await response.HttpResponse.Content.ReadFromJsonAsync<AdminSendNotificationResultDto>(JsonOptions, cancellationToken);
        return result == null
            ? AdminApiResult<AdminSendNotificationResultDto>.Fail("Resposta vazia ao enviar notificacao.")
            : AdminApiResult<AdminSendNotificationResultDto>.Ok(result);
    }

    public async Task<AdminApiResult<Guid>> FindUserIdByEmailAsync(
        string email,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return AdminApiResult<Guid>.Fail("Email do destinatario nao informado.");
        }

        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<Guid>.Fail("ApiBaseUrl nao configurada.");
        }

        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["searchTerm"] = email.Trim(),
            ["page"] = "1",
            ["pageSize"] = "50"
        };

        var nonEmptyQuery = query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        var url = QueryHelpers.AddQueryString($"{baseUrl}/api/admin/users", nonEmptyQuery);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<Guid>.Fail(
                response.ErrorMessage ?? "Falha ao buscar usuario por email.",
                response.ErrorCode,
                response.StatusCode);
        }

        var users = await response.HttpResponse.Content.ReadFromJsonAsync<AdminUsersListResponseDto>(JsonOptions, cancellationToken);
        var target = users?.Items?.FirstOrDefault(u => u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            return AdminApiResult<Guid>.Fail("Usuario destinatario nao encontrado para o email informado.", "not_found");
        }

        return AdminApiResult<Guid>.Ok(target.Id);
    }

    public async Task<AdminApiResult<IReadOnlyList<AdminServiceCategoryDto>>> GetServiceCategoriesAsync(
        bool includeInactive,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<IReadOnlyList<AdminServiceCategoryDto>>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = QueryHelpers.AddQueryString($"{baseUrl}/api/admin/service-categories", new Dictionary<string, string?>
        {
            ["includeInactive"] = includeInactive ? "true" : "false"
        });

        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<IReadOnlyList<AdminServiceCategoryDto>>.Fail(
                response.ErrorMessage ?? "Falha ao consultar categorias de servico.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<IReadOnlyList<AdminServiceCategoryDto>>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<IReadOnlyList<AdminServiceCategoryDto>>.Fail("Resposta vazia da API de categorias.")
            : AdminApiResult<IReadOnlyList<AdminServiceCategoryDto>>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminServiceCategoryUpsertResultDto>> CreateServiceCategoryAsync(
        AdminCreateServiceCategoryRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminServiceCategoryUpsertResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var response = await SendAsync(HttpMethod.Post, $"{baseUrl}/api/admin/service-categories", accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminServiceCategoryUpsertResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao criar categoria.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminServiceCategoryUpsertResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminServiceCategoryUpsertResultDto>.Fail("Resposta vazia da API ao criar categoria.")
            : AdminApiResult<AdminServiceCategoryUpsertResultDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminServiceCategoryUpsertResultDto>> UpdateServiceCategoryAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminServiceCategoryUpsertResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var response = await SendAsync(HttpMethod.Put, $"{baseUrl}/api/admin/service-categories/{categoryId:D}", accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminServiceCategoryUpsertResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao atualizar categoria.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminServiceCategoryUpsertResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminServiceCategoryUpsertResultDto>.Fail("Resposta vazia da API ao atualizar categoria.")
            : AdminApiResult<AdminServiceCategoryUpsertResultDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdateServiceCategoryStatusAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/service-categories/{categoryId:D}/status";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken);
    }

    public async Task<AdminApiResult<IReadOnlyList<AdminChecklistTemplateDto>>> GetChecklistTemplatesAsync(
        bool includeInactive,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<IReadOnlyList<AdminChecklistTemplateDto>>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = QueryHelpers.AddQueryString($"{baseUrl}/api/admin/checklist-templates", new Dictionary<string, string?>
        {
            ["includeInactive"] = includeInactive ? "true" : "false"
        });

        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<IReadOnlyList<AdminChecklistTemplateDto>>.Fail(
                response.ErrorMessage ?? "Falha ao consultar templates de checklist.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<IReadOnlyList<AdminChecklistTemplateDto>>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<IReadOnlyList<AdminChecklistTemplateDto>>.Fail("Resposta vazia da API de checklist.")
            : AdminApiResult<IReadOnlyList<AdminChecklistTemplateDto>>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminChecklistTemplateUpsertResultDto>> CreateChecklistTemplateAsync(
        AdminCreateChecklistTemplateRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var response = await SendAsync(HttpMethod.Post, $"{baseUrl}/api/admin/checklist-templates", accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao criar template de checklist.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminChecklistTemplateUpsertResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Fail("Resposta vazia da API ao criar template de checklist.")
            : AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminChecklistTemplateUpsertResultDto>> UpdateChecklistTemplateAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var response = await SendAsync(HttpMethod.Put, $"{baseUrl}/api/admin/checklist-templates/{templateId:D}", accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao atualizar template de checklist.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminChecklistTemplateUpsertResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Fail("Resposta vazia da API ao atualizar template de checklist.")
            : AdminApiResult<AdminChecklistTemplateUpsertResultDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdateChecklistTemplateStatusAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/checklist-templates/{templateId:D}/status";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken);
    }

    public async Task<AdminApiResult<AdminPlanGovernanceSnapshotDto>> GetPlanGovernanceSnapshotAsync(
        bool includeInactivePromotions,
        bool includeInactiveCoupons,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminPlanGovernanceSnapshotDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = QueryHelpers.AddQueryString($"{baseUrl}/api/admin/plan-governance", new Dictionary<string, string?>
        {
            ["includeInactivePromotions"] = includeInactivePromotions ? "true" : "false",
            ["includeInactiveCoupons"] = includeInactiveCoupons ? "true" : "false"
        });

        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminPlanGovernanceSnapshotDto>.Fail(
                response.ErrorMessage ?? "Falha ao carregar a governanca de planos.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminPlanGovernanceSnapshotDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminPlanGovernanceSnapshotDto>.Fail("Resposta vazia da API de governanca de planos.")
            : AdminApiResult<AdminPlanGovernanceSnapshotDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanSettingAsync(
        string plan,
        AdminUpdatePlanSettingRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/plan-governance/settings/{plan}";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken, HttpMethod.Put);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> CreatePlanPromotionAsync(
        AdminCreatePlanPromotionRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/plan-governance/promotions";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken, HttpMethod.Post);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanPromotionAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/plan-governance/promotions/{promotionId:D}";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken, HttpMethod.Put);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanPromotionStatusAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/plan-governance/promotions/{promotionId:D}/status";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken, HttpMethod.Put);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> CreatePlanCouponAsync(
        AdminCreatePlanCouponRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/plan-governance/coupons";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken, HttpMethod.Post);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanCouponAsync(
        Guid couponId,
        AdminUpdatePlanCouponRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/plan-governance/coupons/{couponId:D}";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken, HttpMethod.Put);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanCouponStatusAsync(
        Guid couponId,
        AdminUpdatePlanCouponStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/plan-governance/coupons/{couponId:D}/status";
        return await SendAdminOperationAsync(url, request, accessToken, cancellationToken, HttpMethod.Put);
    }

    public async Task<AdminApiResult<AdminPlanPriceSimulationResultDto>> SimulatePlanPriceAsync(
        AdminPlanPriceSimulationRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminPlanPriceSimulationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var response = await SendAsync(HttpMethod.Post, $"{baseUrl}/api/admin/plan-governance/simulate", accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminPlanPriceSimulationResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao simular preco do plano.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminPlanPriceSimulationResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminPlanPriceSimulationResultDto>.Fail("Resposta vazia da API de simulacao.")
            : AdminApiResult<AdminPlanPriceSimulationResultDto>.Ok(payload);
    }

    private async Task<AdminApiResult<AdminOperationResultDto>> SendAdminOperationAsync(
        string url,
        object payload,
        string accessToken,
        CancellationToken cancellationToken,
        HttpMethod? method = null)
    {
        var response = await SendAsync(method ?? HttpMethod.Put, url, accessToken, payload, cancellationToken);
        if (!response.Success)
        {
            var errorDto = response.ErrorOperation;
            return AdminApiResult<AdminOperationResultDto>.Fail(
                errorDto?.ErrorMessage ?? response.ErrorMessage ?? "Falha na operacao administrativa.",
                errorDto?.ErrorCode ?? response.ErrorCode,
                response.StatusCode);
        }

        if (response.HttpResponse == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("Resposta invalida da operacao administrativa.");
        }

        var result = await response.HttpResponse.Content.ReadFromJsonAsync<AdminOperationResultDto>(JsonOptions, cancellationToken);
        return result == null
            ? AdminApiResult<AdminOperationResultDto>.Fail("Resposta vazia da operacao administrativa.")
            : AdminApiResult<AdminOperationResultDto>.Ok(result);
    }

    private async Task<ApiCallResult> SendAsync(
        HttpMethod method,
        string url,
        string accessToken,
        object? payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return ApiCallResult.Fail("Sessao expirada. Faca login novamente.", "unauthorized", (int)HttpStatusCode.Unauthorized);
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (payload != null)
        {
            request.Content = JsonContent.Create(payload);
        }

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ApiCallResult.Ok(response);
            }

            var operationError = await TryReadAsync<AdminOperationResultDto>(response, cancellationToken);
            var notificationError = await TryReadAsync<AdminSendNotificationResultDto>(response, cancellationToken);
            var fallbackMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Sessao expirada. Faca login novamente.",
                HttpStatusCode.Forbidden => "Acesso negado ao endpoint administrativo.",
                HttpStatusCode.NotFound => "Registro nao encontrado.",
                HttpStatusCode.Conflict => operationError?.ErrorMessage ?? notificationError?.ErrorMessage ?? "Operacao em conflito com o estado atual.",
                _ => $"Falha ao consultar API admin ({(int)response.StatusCode})."
            };

            return ApiCallResult.Fail(
                operationError?.ErrorMessage ?? notificationError?.ErrorMessage ?? fallbackMessage,
                operationError?.ErrorCode ?? notificationError?.ErrorCode,
                (int)response.StatusCode,
                operationError,
                notificationError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar API admin de operacoes.");
            return ApiCallResult.Fail("Falha de comunicacao com a API administrativa.");
        }
    }

    private static async Task<T?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private string? GetApiBaseUrl()
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        return string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');
    }

    private static string BuildServiceRequestsUrl(string baseUrl, AdminServiceRequestsFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["searchTerm"] = string.IsNullOrWhiteSpace(filters.SearchTerm) ? null : filters.SearchTerm.Trim(),
            ["status"] = NormalizeRequestStatus(filters.Status),
            ["category"] = NormalizeCategory(filters.Category),
            ["page"] = Math.Max(1, filters.Page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(filters.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["fromUtc"] = filters.FromUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["toUtc"] = filters.ToUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
        };

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/service-requests", FilterQuery(query));
    }

    private static string BuildProposalsUrl(string baseUrl, AdminProposalsFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = filters.RequestId?.ToString("D", CultureInfo.InvariantCulture),
            ["providerId"] = filters.ProviderId?.ToString("D", CultureInfo.InvariantCulture),
            ["status"] = NormalizeProposalStatus(filters.Status),
            ["page"] = Math.Max(1, filters.Page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(filters.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["fromUtc"] = filters.FromUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["toUtc"] = filters.ToUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
        };

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/proposals", FilterQuery(query));
    }

    private static string BuildChatsUrl(string baseUrl, AdminChatsFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = filters.RequestId?.ToString("D", CultureInfo.InvariantCulture),
            ["providerId"] = filters.ProviderId?.ToString("D", CultureInfo.InvariantCulture),
            ["clientId"] = filters.ClientId?.ToString("D", CultureInfo.InvariantCulture),
            ["searchTerm"] = string.IsNullOrWhiteSpace(filters.SearchTerm) ? null : filters.SearchTerm.Trim(),
            ["page"] = Math.Max(1, filters.Page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(filters.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["fromUtc"] = filters.FromUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["toUtc"] = filters.ToUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
        };

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/chats", FilterQuery(query));
    }

    private static string BuildChatAttachmentsUrl(string baseUrl, AdminChatAttachmentsFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = filters.RequestId?.ToString("D", CultureInfo.InvariantCulture),
            ["userId"] = filters.UserId?.ToString("D", CultureInfo.InvariantCulture),
            ["mediaKind"] = string.IsNullOrWhiteSpace(filters.MediaKind) ? null : filters.MediaKind.Trim().ToLowerInvariant(),
            ["page"] = Math.Max(1, filters.Page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(filters.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["fromUtc"] = filters.FromUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["toUtc"] = filters.ToUtc?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
        };

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/chat-attachments", FilterQuery(query));
    }

    private static string BuildDisputesQueueUrl(string baseUrl, AdminDisputesQueueFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["disputeCaseId"] = filters.DisputeCaseId?.ToString("D", CultureInfo.InvariantCulture),
            ["take"] = Math.Clamp(filters.Take, 1, 200).ToString(CultureInfo.InvariantCulture),
            ["status"] = string.IsNullOrWhiteSpace(filters.Status) ? null : filters.Status.Trim(),
            ["type"] = string.IsNullOrWhiteSpace(filters.Type) ? null : filters.Type.Trim(),
            ["operatorAdminId"] = filters.OperatorAdminId?.ToString("D", CultureInfo.InvariantCulture),
            ["operatorScope"] = string.IsNullOrWhiteSpace(filters.OperatorScope) || filters.OperatorScope.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : filters.OperatorScope.Trim(),
            ["sla"] = string.IsNullOrWhiteSpace(filters.Sla) || filters.Sla.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : filters.Sla.Trim()
        };

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/disputes/queue", FilterQuery(query));
    }

    private static string BuildDisputesQueueExportUrl(string baseUrl, AdminDisputesQueueFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["disputeCaseId"] = filters.DisputeCaseId?.ToString("D", CultureInfo.InvariantCulture),
            ["take"] = Math.Clamp(filters.Take, 1, 200).ToString(CultureInfo.InvariantCulture),
            ["status"] = string.IsNullOrWhiteSpace(filters.Status) ? null : filters.Status.Trim(),
            ["type"] = string.IsNullOrWhiteSpace(filters.Type) ? null : filters.Type.Trim(),
            ["operatorAdminId"] = filters.OperatorAdminId?.ToString("D", CultureInfo.InvariantCulture),
            ["operatorScope"] = string.IsNullOrWhiteSpace(filters.OperatorScope) || filters.OperatorScope.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : filters.OperatorScope.Trim(),
            ["sla"] = string.IsNullOrWhiteSpace(filters.Sla) || filters.Sla.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : filters.Sla.Trim()
        };

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/disputes/queue/export", FilterQuery(query));
    }

    private static Dictionary<string, string?> FilterQuery(Dictionary<string, string?> query)
    {
        return query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeRequestStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var normalized = status.Trim().ToLowerInvariant();
        return normalized == "all" ? null : status.Trim();
    }

    private static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        var normalized = category.Trim().ToLowerInvariant();
        return normalized == "all" ? null : category.Trim();
    }

    private static string? NormalizeProposalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var normalized = status.Trim().ToLowerInvariant();
        return normalized == "all" ? null : normalized;
    }

    private class ApiCallResult
    {
        public bool Success { get; init; }
        public HttpResponseMessage? HttpResponse { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
        public int? StatusCode { get; init; }
        public AdminOperationResultDto? ErrorOperation { get; init; }
        public AdminSendNotificationResultDto? ErrorNotification { get; init; }

        public static ApiCallResult Ok(HttpResponseMessage response)
            => new() { Success = true, HttpResponse = response };

        public static ApiCallResult Fail(
            string message,
            string? errorCode = null,
            int? statusCode = null,
            AdminOperationResultDto? operationError = null,
            AdminSendNotificationResultDto? notificationError = null)
            => new()
            {
                Success = false,
                ErrorMessage = message,
                ErrorCode = errorCode,
                StatusCode = statusCode,
                ErrorOperation = operationError,
                ErrorNotification = notificationError
            };
    }
}
