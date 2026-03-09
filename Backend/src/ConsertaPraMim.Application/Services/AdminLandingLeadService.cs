using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public sealed class AdminLandingLeadService : IAdminLandingLeadService
{
    private readonly ILandingLeadRepository _landingLeadRepository;

    public AdminLandingLeadService(ILandingLeadRepository landingLeadRepository)
    {
        _landingLeadRepository = landingLeadRepository;
    }

    public async Task<AdminLandingLeadsListResponseDto> GetLandingLeadsAsync(AdminLandingLeadsQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);

        var leads = (await _landingLeadRepository.GetAllAsync()).AsQueryable();

        if (TryParseOrigin(query.Origin, out var parsedOrigin))
        {
            leads = leads.Where(lead => lead.Origin == parsedOrigin);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            leads = leads.Where(lead => lead.City.Contains(city, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var state = query.State.Trim();
            leads = leads.Where(lead => lead.State.Equals(state, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim();
            leads = leads.Where(lead =>
                lead.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                lead.Phone.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                lead.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                lead.City.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                lead.State.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                lead.Neighborhood.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(lead.ServiceCategory) && lead.ServiceCategory.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(lead.RequestedService) && lead.RequestedService.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(lead.CompanyName) && lead.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(lead.UtmCampaign) && lead.UtmCampaign.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        leads = leads.Where(lead => lead.CreatedAt >= fromUtc && lead.CreatedAt <= toUtc);

        var filtered = leads
            .OrderByDescending(lead => lead.CreatedAt)
            .ToList();

        var totalCount = filtered.Count;
        var totalClientLeads = filtered.Count(lead => lead.Origin == LandingLeadOrigin.Client);
        var totalProviderLeads = filtered.Count(lead => lead.Origin == LandingLeadOrigin.Provider);

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(lead => new AdminLandingLeadListItemDto(
                lead.Id,
                lead.Origin,
                lead.FullName,
                lead.Phone,
                lead.Email,
                BuildLocality(lead),
                lead.City,
                lead.State,
                lead.Neighborhood,
                ResolvePrimaryInterest(lead),
                lead.UtmCampaign,
                lead.CreatedAt))
            .ToList();

        return new AdminLandingLeadsListResponseDto(
            page,
            pageSize,
            totalCount,
            totalClientLeads,
            totalProviderLeads,
            items);
    }

    public async Task<AdminLandingLeadDetailsDto?> GetLandingLeadByIdAsync(Guid leadId)
    {
        var lead = await _landingLeadRepository.GetByIdAsync(leadId);
        if (lead == null)
        {
            return null;
        }

        return new AdminLandingLeadDetailsDto(
            lead.Id,
            lead.Origin,
            lead.FullName,
            lead.Phone,
            lead.Email,
            lead.City,
            lead.State,
            lead.Neighborhood,
            BuildLocality(lead),
            lead.ServiceCategory,
            lead.RequestedService,
            lead.CompanyName,
            lead.CompanyDocument,
            lead.YearsOfExperience,
            lead.Message,
            lead.CurrentPageUrl,
            lead.ReferrerUrl,
            lead.Host,
            lead.Scheme,
            lead.Path,
            lead.QueryString,
            lead.UtmSource,
            lead.UtmMedium,
            lead.UtmCampaign,
            lead.UtmTerm,
            lead.UtmContent,
            lead.IpAddress,
            lead.ForwardedFor,
            lead.UserAgent,
            lead.AcceptLanguage,
            lead.BrowserLanguage,
            lead.ScreenResolution,
            lead.DevicePlatform,
            lead.TimeZone,
            lead.MetadataJson,
            lead.CreatedAt,
            lead.UpdatedAt);
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var from = fromUtc?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30);
        var to = toUtc?.ToUniversalTime() ?? DateTime.UtcNow.AddMinutes(1);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        return (from, to);
    }

    private static bool TryParseOrigin(string? rawOrigin, out LandingLeadOrigin origin)
    {
        origin = default;
        if (string.IsNullOrWhiteSpace(rawOrigin))
        {
            return false;
        }

        var normalized = rawOrigin.Trim();
        if (normalized.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Enum.TryParse(normalized, true, out origin);
    }

    private static string BuildLocality(LandingLead lead)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(lead.Neighborhood))
        {
            parts.Add(lead.Neighborhood.Trim());
        }

        var cityState = string.Join(
            "/",
            new[] { lead.City?.Trim(), lead.State?.Trim()?.ToUpperInvariant() }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(cityState))
        {
            parts.Add(cityState);
        }

        return parts.Count == 0 ? "Nao informado" : string.Join(" - ", parts);
    }

    private static string? ResolvePrimaryInterest(LandingLead lead)
    {
        return FirstNonEmpty(
            lead.RequestedService,
            lead.ServiceCategory,
            lead.CompanyName,
            lead.Message);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
