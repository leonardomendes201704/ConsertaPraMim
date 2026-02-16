using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminProviderCreditsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;
    private readonly IAdminUsersApiClient _adminUsersApiClient;

    public AdminProviderCreditsController(
        IAdminOperationsApiClient adminOperationsApiClient,
        IAdminUsersApiClient adminUsersApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
        _adminUsersApiClient = adminUsersApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? providerEmail,
        Guid? providerId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? entryType,
        string? status,
        string? searchTerm,
        int page = 1,
        int pageSize = 20)
    {
        var model = new AdminProviderCreditsIndexViewModel
        {
            Filters = NormalizeFilters(providerEmail, providerId, fromUtc, toUtc, entryType, status, searchTerm, page, pageSize)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var usageReportResult = await _adminOperationsApiClient.GetProviderCreditUsageReportAsync(
            new AdminProviderCreditUsageReportQueryDto(
                FromUtc: model.Filters.FromUtc,
                ToUtc: model.Filters.ToUtc,
                EntryType: TryParseEntryType(model.Filters.EntryType, out var parsedEntryType) ? parsedEntryType : null,
                Status: model.Filters.Status,
                SearchTerm: model.Filters.SearchTerm,
                Page: model.Filters.Page,
                PageSize: model.Filters.PageSize),
            token,
            HttpContext.RequestAborted);

        if (usageReportResult.Success && usageReportResult.Data != null)
        {
            model.UsageReport = usageReportResult.Data;
        }
        else
        {
            model.ReportErrorMessage = usageReportResult.ErrorMessage ?? "Nao foi possivel carregar o relatorio administrativo de creditos.";
        }

        var resolvedProviderId = model.Filters.ProviderId;
        if (!resolvedProviderId.HasValue && !string.IsNullOrWhiteSpace(model.Filters.ProviderEmail))
        {
            var lookup = await _adminOperationsApiClient.FindUserIdByEmailAsync(
                model.Filters.ProviderEmail!,
                token,
                HttpContext.RequestAborted);

            if (!lookup.Success || lookup.Data == Guid.Empty)
            {
                model.ErrorMessage = lookup.ErrorMessage ?? "Nao foi possivel localizar o prestador pelo email informado.";
                return View(model);
            }

            resolvedProviderId = lookup.Data;
            model.Filters.ProviderId = resolvedProviderId;
        }

        if (!resolvedProviderId.HasValue)
        {
            model.InfoMessage = "Informe o email do prestador para consultar saldo/extrato individual. O relatorio consolidado permanece disponivel abaixo.";
            return View(model);
        }

        var userResult = await _adminUsersApiClient.GetUserByIdAsync(resolvedProviderId.Value, token, HttpContext.RequestAborted);
        if (!userResult.Success || userResult.Data == null)
        {
            model.ErrorMessage = userResult.ErrorMessage ?? "Nao foi possivel carregar os dados do prestador.";
            return View(model);
        }

        if (!string.Equals(userResult.Data.Role, "Provider", StringComparison.OrdinalIgnoreCase))
        {
            model.ErrorMessage = "O usuario informado nao pertence ao perfil de prestador.";
            return View(model);
        }

        model.Provider = userResult.Data;
        if (string.IsNullOrWhiteSpace(model.Filters.ProviderEmail))
        {
            model.Filters.ProviderEmail = userResult.Data.Email;
        }

        var balanceResult = await _adminOperationsApiClient.GetProviderCreditBalanceAsync(
            resolvedProviderId.Value,
            token,
            HttpContext.RequestAborted);
        if (!balanceResult.Success || balanceResult.Data == null)
        {
            model.ErrorMessage = balanceResult.ErrorMessage ?? "Nao foi possivel carregar o saldo de creditos.";
            return View(model);
        }

        model.Balance = balanceResult.Data;

        var statementResult = await _adminOperationsApiClient.GetProviderCreditStatementAsync(
            resolvedProviderId.Value,
            model.Filters,
            token,
            HttpContext.RequestAborted);
        if (!statementResult.Success || statementResult.Data == null)
        {
            model.ErrorMessage = statementResult.ErrorMessage ?? "Nao foi possivel carregar o extrato de creditos.";
            return View(model);
        }

        var statement = statementResult.Data;
        if (!string.Equals(model.Filters.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var filteredItems = statement.Items
                .Where(item => MatchesStatus(item.EntryType, model.Filters.Status))
                .ToList();

            statement = statement with
            {
                Items = filteredItems,
                Page = 1,
                PageSize = Math.Max(1, filteredItems.Count),
                TotalCount = filteredItems.Count
            };

            model.Filters.Page = 1;
        }

        model.Statement = statement;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Grant([FromBody] AdminProviderCreditGrantWebRequest request)
    {
        if (request == null || request.ProviderId == Guid.Empty || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new { success = false, errorMessage = "Payload invalido para concessao de credito." });
        }

        if (!TryParseGrantType(request.GrantType, out var grantType))
        {
            return BadRequest(new { success = false, errorMessage = "Tipo de concessao invalido." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminProviderCreditGrantRequestDto(
            request.ProviderId,
            request.Amount,
            request.Reason,
            grantType,
            NormalizeUtc(request.ExpiresAtUtc),
            request.Notes,
            request.CampaignCode);

        var result = await _adminOperationsApiClient.GrantProviderCreditAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null || !result.Data.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel conceder o credito.",
                errorCode = result.Data?.ErrorCode ?? result.ErrorCode
            });
        }

        return Ok(new { success = true, mutation = result.Data });
    }

    [HttpPost]
    public async Task<IActionResult> Reverse([FromBody] AdminProviderCreditReversalWebRequest request)
    {
        if (request == null || request.ProviderId == Guid.Empty || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new { success = false, errorMessage = "Payload invalido para estorno de credito." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminProviderCreditReversalRequestDto(
            request.ProviderId,
            request.Amount,
            request.Reason,
            request.OriginalEntryId,
            request.Notes);

        var result = await _adminOperationsApiClient.ReverseProviderCreditAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null || !result.Data.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel estornar o credito.",
                errorCode = result.Data?.ErrorCode ?? result.ErrorCode
            });
        }

        return Ok(new { success = true, mutation = result.Data });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminProviderCreditsFilterModel NormalizeFilters(
        string? providerEmail,
        Guid? providerId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? entryType,
        string? status,
        string? searchTerm,
        int page,
        int pageSize)
    {
        return new AdminProviderCreditsFilterModel
        {
            ProviderEmail = string.IsNullOrWhiteSpace(providerEmail) ? null : providerEmail.Trim(),
            ProviderId = providerId.HasValue && providerId.Value != Guid.Empty ? providerId : null,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EntryType = NormalizeEntryType(entryType),
            Status = NormalizeStatus(status),
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        };
    }

    private static string NormalizeEntryType(string? entryType)
    {
        if (string.IsNullOrWhiteSpace(entryType))
        {
            return "all";
        }

        var normalized = entryType.Trim().ToLowerInvariant();
        return normalized is "grant" or "debit" or "expire" or "reversal"
            ? normalized
            : "all";
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "all";
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized is "credit" or "debit" ? normalized : "all";
    }

    private static bool MatchesStatus(ProviderCreditLedgerEntryType entryType, string status)
    {
        return status.ToLowerInvariant() switch
        {
            "credit" => entryType is ProviderCreditLedgerEntryType.Grant or ProviderCreditLedgerEntryType.Reversal,
            "debit" => entryType is ProviderCreditLedgerEntryType.Debit or ProviderCreditLedgerEntryType.Expire,
            _ => true
        };
    }

    private static bool TryParseEntryType(string? raw, out ProviderCreditLedgerEntryType? entryType)
    {
        entryType = null;
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Enum.TryParse<ProviderCreditLedgerEntryType>(raw.Trim(), true, out var parsed))
        {
            entryType = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseGrantType(string? value, out ProviderCreditGrantType grantType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            grantType = ProviderCreditGrantType.Premio;
            return false;
        }

        if (!Enum.TryParse(value.Trim(), true, out grantType))
        {
            return false;
        }

        return grantType is ProviderCreditGrantType.Premio or ProviderCreditGrantType.Campanha or ProviderCreditGrantType.Ajuste;
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
