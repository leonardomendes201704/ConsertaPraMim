using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class PjRecurringContractService : IPjRecurringContractService
{
    private readonly IPjRecurringContractRepository _pjRecurringContractRepository;
    private readonly IUserRepository _userRepository;

    public PjRecurringContractService(
        IPjRecurringContractRepository pjRecurringContractRepository,
        IUserRepository userRepository)
    {
        _pjRecurringContractRepository = pjRecurringContractRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<PjRecurringContractDto>> GetClientContractsAsync(
        Guid clientUserId,
        CancellationToken cancellationToken = default)
    {
        var contracts = await _pjRecurringContractRepository.ListByClientUserIdAsync(clientUserId, cancellationToken);
        var providers = await GetActiveProviderEligibilityPoolAsync(cancellationToken);
        return contracts
            .Select(contract => MapDto(
                contract,
                CountEligibleProviders(providers, contract.Category, contract.ProviderEligibility)))
            .ToList();
    }

    public async Task<AdminPjRecurringPortfolioDto> GetAdminPortfolioAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        PjRecurringContractStatus? status,
        CancellationToken cancellationToken = default)
    {
        var normalizedFromUtc = fromUtc?.ToUniversalTime();
        var normalizedToUtc = toUtc?.ToUniversalTime();
        if (normalizedFromUtc.HasValue &&
            normalizedToUtc.HasValue &&
            normalizedFromUtc.Value > normalizedToUtc.Value)
        {
            throw new InvalidOperationException("Janela de consulta invalida: fromUtc deve ser menor ou igual a toUtc.");
        }

        var contracts = await _pjRecurringContractRepository.ListAllAsync(cancellationToken);
        var filteredContracts = contracts
            .Where(contract =>
                (!normalizedFromUtc.HasValue || contract.CreatedAt >= normalizedFromUtc.Value) &&
                (!normalizedToUtc.HasValue || contract.CreatedAt <= normalizedToUtc.Value) &&
                (!status.HasValue || contract.Status == status.Value))
            .OrderByDescending(contract => contract.CreatedAt)
            .ToList();

        var providers = await GetActiveProviderEligibilityPoolAsync(cancellationToken);
        var users = await _userRepository.GetAllAsync();
        var clientNamesById = users
            .Where(user => user.Role == UserRole.Client)
            .GroupBy(user => user.Id)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var name = group.First().Name;
                    return string.IsNullOrWhiteSpace(name) ? "Cliente sem nome" : name.Trim();
                });

        var items = filteredContracts
            .Take(200)
            .Select(contract =>
            {
                var eligibleProvidersCount = CountEligibleProviders(providers, contract.Category, contract.ProviderEligibility);
                var clientName = clientNamesById.TryGetValue(contract.ClientUserId, out var name)
                    ? name
                    : "Cliente nao localizado";

                return new AdminPjRecurringPortfolioItemDto(
                    contract.Id,
                    contract.ClientUserId,
                    clientName,
                    contract.ClientPjType,
                    contract.Category,
                    contract.ProviderEligibility,
                    contract.Status,
                    contract.MonthlyAmount,
                    contract.IncludedVisitsPerCycle,
                    contract.ResponseSlaHours,
                    contract.StartsAtUtc,
                    contract.NextRenewalAtUtc,
                    contract.EndsAtUtc,
                    contract.LastPaymentAtUtc,
                    contract.AutoRenew,
                    eligibleProvidersCount);
            })
            .ToList();

        var activeContracts = filteredContracts.Count(contract => contract.Status == PjRecurringContractStatus.Active);
        var delinquentContracts = filteredContracts.Count(contract => contract.Status == PjRecurringContractStatus.Delinquent);
        var recurringRevenueBase = filteredContracts
            .Where(contract => contract.Status is PjRecurringContractStatus.Active or PjRecurringContractStatus.Delinquent)
            .ToList();
        var monthlyRecurringRevenue = recurringRevenueBase.Sum(contract => contract.MonthlyAmount);
        var averageTicket = recurringRevenueBase.Count > 0
            ? decimal.Round(monthlyRecurringRevenue / recurringRevenueBase.Count, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var statusBreakdown = filteredContracts
            .GroupBy(contract => contract.Status)
            .OrderBy(group => group.Key)
            .Select(group => new AdminPjRecurringStatusBreakdownDto(
                group.Key,
                group.Count(),
                decimal.Round(group.Sum(contract => contract.MonthlyAmount), 2, MidpointRounding.AwayFromZero)))
            .ToList();

        var categoryBreakdown = filteredContracts
            .GroupBy(contract => contract.Category)
            .OrderByDescending(group => group.Sum(contract => contract.MonthlyAmount))
            .ThenBy(group => group.Key)
            .Select(group => new AdminPjRecurringCategoryBreakdownDto(
                group.Key,
                group.Count(),
                decimal.Round(group.Sum(contract => contract.MonthlyAmount), 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new AdminPjRecurringPortfolioDto(
            GeneratedAtUtc: DateTime.UtcNow,
            TotalContracts: filteredContracts.Count,
            ActiveContracts: activeContracts,
            DelinquentContracts: delinquentContracts,
            MonthlyRecurringRevenue: decimal.Round(monthlyRecurringRevenue, 2, MidpointRounding.AwayFromZero),
            AverageTicket: averageTicket,
            StatusBreakdown: statusBreakdown,
            CategoryBreakdown: categoryBreakdown,
            Contracts: items);
    }

    public async Task<PjRecurringContractDto> CreateAsync(
        Guid clientUserId,
        CreatePjRecurringContractRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await EnsurePjClientAsync(clientUserId);
        ValidateCreateRequest(request);

        var clientPjType = user.ClientPjType ?? request.ClientPjType;
        if (user.ClientPjType.HasValue && user.ClientPjType.Value != request.ClientPjType)
        {
            throw new InvalidOperationException("Tipo PJ informado diverge do perfil do cliente autenticado.");
        }

        var startsAtUtc = request.StartsAtUtc.ToUniversalTime();
        var nextRenewalAtUtc = CalculateNextRenewal(startsAtUtc, request.Cadence);
        var providers = await GetActiveProviderEligibilityPoolAsync(cancellationToken);
        var eligibleProvidersCount = CountEligibleProviders(providers, request.Category, request.ProviderEligibility);
        if (eligibleProvidersCount <= 0)
        {
            throw new InvalidOperationException("Nao existem prestadores elegiveis para o pacote PJ informado.");
        }

        var contract = new PjRecurringContract
        {
            ClientUserId = clientUserId,
            ClientPjType = clientPjType,
            Category = request.Category,
            ProviderEligibility = request.ProviderEligibility,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Cadence = request.Cadence,
            Status = PjRecurringContractStatus.Active,
            MonthlyAmount = decimal.Round(request.MonthlyAmount, 2, MidpointRounding.AwayFromZero),
            IncludedVisitsPerCycle = request.IncludedVisitsPerCycle,
            ResponseSlaHours = request.ResponseSlaHours,
            OperationalWindowStartMinute = request.OperationalWindowStartMinute,
            OperationalWindowEndMinute = request.OperationalWindowEndMinute,
            OperationalDaysMask = request.OperationalDaysMask,
            StartsAtUtc = startsAtUtc,
            NextRenewalAtUtc = nextRenewalAtUtc,
            EndsAtUtc = request.EndsAtUtc?.ToUniversalTime(),
            AutoRenew = request.AutoRenew
        };

        if (contract.EndsAtUtc.HasValue && contract.EndsAtUtc.Value < contract.NextRenewalAtUtc)
        {
            throw new InvalidOperationException("A data final do contrato deve permitir ao menos um ciclo de renovacao.");
        }

        await _pjRecurringContractRepository.AddAsync(contract, cancellationToken);
        return MapDto(contract, eligibleProvidersCount);
    }

    public async Task<PjRecurringContractDto> RenewAsync(
        Guid clientUserId,
        Guid contractId,
        RenewPjRecurringContractRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePjClientAsync(clientUserId);

        var contract = await _pjRecurringContractRepository.GetByIdAsync(contractId, cancellationToken);
        if (contract == null)
        {
            throw new KeyNotFoundException("Contrato recorrente PJ nao encontrado.");
        }

        if (contract.ClientUserId != clientUserId)
        {
            throw new UnauthorizedAccessException("Contrato nao pertence ao cliente autenticado.");
        }

        if (contract.Status is PjRecurringContractStatus.Cancelled or PjRecurringContractStatus.Completed)
        {
            throw new InvalidOperationException("Contrato nao pode ser renovado no status atual.");
        }

        var renewedAtUtc = (request.RenewedAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        if (renewedAtUtc < contract.StartsAtUtc)
        {
            throw new InvalidOperationException("Data de renovacao invalida para o contrato.");
        }

        contract.LastRenewedAtUtc = renewedAtUtc;
        contract.LastPaymentAtUtc = renewedAtUtc;
        contract.UpdatedAt = DateTime.UtcNow;

        var renewalBase = contract.NextRenewalAtUtc > renewedAtUtc
            ? contract.NextRenewalAtUtc
            : renewedAtUtc;
        var nextRenewal = CalculateNextRenewal(renewalBase, contract.Cadence);

        if (contract.EndsAtUtc.HasValue && nextRenewal > contract.EndsAtUtc.Value)
        {
            contract.Status = PjRecurringContractStatus.Completed;
            contract.AutoRenew = false;
            contract.NextRenewalAtUtc = contract.EndsAtUtc.Value;
        }
        else
        {
            contract.Status = PjRecurringContractStatus.Active;
            contract.NextRenewalAtUtc = nextRenewal;
        }

        await _pjRecurringContractRepository.UpdateAsync(contract, cancellationToken);
        var providers = await GetActiveProviderEligibilityPoolAsync(cancellationToken);
        var eligibleProvidersCount = CountEligibleProviders(providers, contract.Category, contract.ProviderEligibility);
        return MapDto(contract, eligibleProvidersCount);
    }

    private async Task<User> EnsurePjClientAsync(Guid clientUserId)
    {
        var user = await _userRepository.GetByIdAsync(clientUserId);
        if (user == null || !user.IsActive || user.Role != UserRole.Client)
        {
            throw new UnauthorizedAccessException("Cliente invalido para operar contratos recorrentes PJ.");
        }

        if (user.ClientProfileType != ClientProfileType.Pj)
        {
            throw new InvalidOperationException("Apenas clientes PJ podem contratar pacotes recorrentes.");
        }

        return user;
    }

    private static void ValidateCreateRequest(CreatePjRecurringContractRequestDto request)
    {
        if (request.ProviderEligibility == ProviderClientPreference.PfOnly)
        {
            throw new InvalidOperationException("Pacotes PJ nao podem restringir atendimento somente a PF.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length < 5)
        {
            throw new InvalidOperationException("Titulo do pacote PJ deve possuir ao menos 5 caracteres.");
        }

        if (request.MonthlyAmount < 0)
        {
            throw new InvalidOperationException("Valor mensal do pacote PJ nao pode ser negativo.");
        }

        if (request.IncludedVisitsPerCycle <= 0)
        {
            throw new InvalidOperationException("Informe ao menos uma visita por ciclo.");
        }

        if (request.ResponseSlaHours <= 0 || request.ResponseSlaHours > 168)
        {
            throw new InvalidOperationException("SLA de resposta deve estar entre 1 e 168 horas.");
        }

        if (request.OperationalWindowStartMinute < 0 ||
            request.OperationalWindowStartMinute > 1439 ||
            request.OperationalWindowEndMinute <= request.OperationalWindowStartMinute ||
            request.OperationalWindowEndMinute > 1440)
        {
            throw new InvalidOperationException("Janela operacional invalida.");
        }

        if (request.OperationalDaysMask < 1 || request.OperationalDaysMask > 127)
        {
            throw new InvalidOperationException("Mascara de dias operacionais deve estar entre 1 e 127.");
        }
    }

    private static DateTime CalculateNextRenewal(DateTime baseUtc, PjRecurringCadence cadence)
    {
        return cadence switch
        {
            PjRecurringCadence.Weekly => baseUtc.AddDays(7),
            PjRecurringCadence.Biweekly => baseUtc.AddDays(14),
            PjRecurringCadence.Monthly => baseUtc.AddMonths(1),
            PjRecurringCadence.Quarterly => baseUtc.AddMonths(3),
            PjRecurringCadence.SemiAnnual => baseUtc.AddMonths(6),
            PjRecurringCadence.Annual => baseUtc.AddYears(1),
            _ => baseUtc.AddMonths(1)
        };
    }

    private async Task<IReadOnlyList<ProviderEligibilitySnapshot>> GetActiveProviderEligibilityPoolAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var users = await _userRepository.GetAllAsync();
        return users
            .Where(user =>
                user.Role == UserRole.Provider &&
                user.IsActive &&
                user.ProviderProfile != null &&
                user.ProviderProfile.Categories.Count > 0)
            .Select(user => new ProviderEligibilitySnapshot(
                user.Id,
                user.ProviderProfile!.ClientPreference,
                user.ProviderProfile.Categories.ToHashSet()))
            .ToList();
    }

    private static int CountEligibleProviders(
        IReadOnlyList<ProviderEligibilitySnapshot> providers,
        ServiceCategory category,
        ProviderClientPreference providerEligibility)
    {
        return providers.Count(provider =>
            provider.Categories.Contains(category) &&
            CanProviderServePjContract(provider.ClientPreference, providerEligibility));
    }

    private static bool CanProviderServePjContract(
        ProviderClientPreference providerPreference,
        ProviderClientPreference contractEligibility)
    {
        if (providerPreference == ProviderClientPreference.PfOnly)
        {
            return false;
        }

        return contractEligibility switch
        {
            ProviderClientPreference.PjOnly => providerPreference == ProviderClientPreference.PjOnly,
            ProviderClientPreference.Both => providerPreference == ProviderClientPreference.Both || providerPreference == ProviderClientPreference.PjOnly,
            _ => false
        };
    }

    private static PjRecurringContractDto MapDto(PjRecurringContract contract, int eligibleProvidersCount)
    {
        return new PjRecurringContractDto(
            contract.Id,
            contract.ClientUserId,
            contract.ClientPjType,
            contract.Category,
            contract.ProviderEligibility,
            contract.Title,
            contract.Description,
            contract.Cadence,
            contract.Status,
            contract.MonthlyAmount,
            contract.IncludedVisitsPerCycle,
            contract.ResponseSlaHours,
            contract.OperationalWindowStartMinute,
            contract.OperationalWindowEndMinute,
            contract.OperationalDaysMask,
            contract.StartsAtUtc,
            contract.NextRenewalAtUtc,
            contract.EndsAtUtc,
            contract.LastRenewedAtUtc,
            contract.LastPaymentAtUtc,
            contract.AutoRenew,
            contract.CancellationReason,
            contract.CreatedAt,
            contract.UpdatedAt,
            eligibleProvidersCount);
    }

    private sealed record ProviderEligibilitySnapshot(
        Guid ProviderId,
        ProviderClientPreference ClientPreference,
        HashSet<ServiceCategory> Categories);
}
