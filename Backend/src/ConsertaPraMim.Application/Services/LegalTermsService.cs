using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class LegalTermsService : ILegalTermsService
{
    private const int MaxTitleLength = 240;
    private const int MaxChangeSummaryLength = 500;
    private const int MaxHtmlLength = 250_000;
    private const string AuditTargetType = "LegalTerms";

    private readonly ILegalTermsRepository _legalTermsRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;

    public LegalTermsService(
        ILegalTermsRepository legalTermsRepository,
        IAdminAuditLogRepository adminAuditLogRepository)
    {
        _legalTermsRepository = legalTermsRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
    }

    public async Task<LegalTermsDocumentDto?> GetActiveAsync(
        LegalTermsAudience audience,
        CancellationToken cancellationToken = default)
    {
        var active = await _legalTermsRepository.GetActiveByAudienceAsync(audience, cancellationToken);
        return active == null ? null : Map(active);
    }

    public async Task<IReadOnlyList<LegalTermsDocumentDto>> GetVersionsAsync(
        LegalTermsAudience audience,
        CancellationToken cancellationToken = default)
    {
        var items = await _legalTermsRepository.ListByAudienceAsync(
            audience,
            asNoTracking: true,
            cancellationToken: cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<LegalTermsPublishResultDto> PublishAsync(
        LegalTermsAudience audience,
        LegalTermsPublishPayloadDto payload,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        var title = Normalize(payload.Title, MaxTitleLength);
        var htmlContent = payload.HtmlContent?.Trim() ?? string.Empty;
        var changeSummary = Normalize(payload.ChangeSummary, MaxChangeSummaryLength);

        if (string.IsNullOrWhiteSpace(title))
        {
            return new LegalTermsPublishResultDto(
                false,
                ErrorCode: "legal_terms_title_required",
                ErrorMessage: "Titulo obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            return new LegalTermsPublishResultDto(
                false,
                ErrorCode: "legal_terms_html_required",
                ErrorMessage: "Conteudo HTML obrigatorio.");
        }

        if (htmlContent.Length > MaxHtmlLength)
        {
            return new LegalTermsPublishResultDto(
                false,
                ErrorCode: "legal_terms_html_too_large",
                ErrorMessage: $"Conteudo HTML excede limite de {MaxHtmlLength} caracteres.");
        }

        var nowUtc = DateTime.UtcNow;
        var versions = await _legalTermsRepository.ListByAudienceAsync(
            audience,
            asNoTracking: false,
            cancellationToken: cancellationToken);

        foreach (var version in versions.Where(x => x.IsPublished))
        {
            version.IsPublished = false;
            version.UpdatedAt = nowUtc;
        }

        var nextVersion = versions.Count == 0
            ? 1
            : versions.Max(x => x.Version) + 1;

        var published = new LegalTermsDocument
        {
            Audience = audience,
            Version = nextVersion,
            Title = title,
            HtmlContent = htmlContent,
            ChangeSummary = changeSummary,
            IsPublished = true,
            PublishedAtUtc = nowUtc,
            PublishedByUserId = actorUserId == Guid.Empty ? null : actorUserId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        await _legalTermsRepository.AddAsync(published, cancellationToken);
        await _legalTermsRepository.SaveChangesAsync(cancellationToken);

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = Normalize(actorEmail, 320) ?? "admin@unknown",
            Action = "LegalTermsPublished",
            TargetType = AuditTargetType,
            TargetId = published.Id,
            Metadata = JsonSerializer.Serialize(new
            {
                audience = ToAudienceKey(audience),
                published.Version,
                published.Title,
                published.PublishedAtUtc
            })
        });

        return new LegalTermsPublishResultDto(
            true,
            Document: Map(published));
    }

    private static LegalTermsDocumentDto Map(LegalTermsDocument document)
    {
        return new LegalTermsDocumentDto(
            Id: document.Id,
            Audience: ToAudienceKey(document.Audience),
            Version: document.Version,
            Title: document.Title,
            HtmlContent: document.HtmlContent,
            ChangeSummary: document.ChangeSummary,
            IsPublished: document.IsPublished,
            PublishedAtUtc: document.PublishedAtUtc,
            CreatedAt: document.CreatedAt,
            UpdatedAt: document.UpdatedAt);
    }

    public static bool TryParseAudience(string? raw, out LegalTermsAudience audience)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            audience = LegalTermsAudience.Client;
            return false;
        }

        var normalized = raw.Trim();
        if (normalized.Equals("client", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("cliente", StringComparison.OrdinalIgnoreCase))
        {
            audience = LegalTermsAudience.Client;
            return true;
        }

        if (normalized.Equals("provider", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("prestador", StringComparison.OrdinalIgnoreCase))
        {
            audience = LegalTermsAudience.Provider;
            return true;
        }

        return Enum.TryParse(normalized, ignoreCase: true, out audience) &&
               audience is LegalTermsAudience.Client or LegalTermsAudience.Provider;
    }

    public static string ToAudienceKey(LegalTermsAudience audience)
    {
        return audience switch
        {
            LegalTermsAudience.Client => "client",
            LegalTermsAudience.Provider => "provider",
            _ => "unknown"
        };
    }

    private static string? Normalize(string? raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }
}
