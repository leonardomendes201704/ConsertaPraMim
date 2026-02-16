using System.Text.Json;
using ConsertaPraMim.Application.Configuration;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class PlanGovernanceService : IPlanGovernanceService
{
    private static readonly IReadOnlyList<ProviderPlan> ManagedPlans = new[]
    {
        ProviderPlan.Bronze,
        ProviderPlan.Silver,
        ProviderPlan.Gold
    };

    private static readonly IReadOnlyList<ServiceCategory> AllCategories = Enum
        .GetValues(typeof(ServiceCategory))
        .Cast<ServiceCategory>()
        .OrderBy(x => (int)x)
        .ToList();

    private readonly IProviderPlanGovernanceRepository _planGovernanceRepository;
    private readonly IProviderCreditRepository _providerCreditRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PlanGovernanceService> _logger;

    public PlanGovernanceService(
        IProviderPlanGovernanceRepository planGovernanceRepository,
        IProviderCreditRepository providerCreditRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        IUserRepository userRepository,
        ILogger<PlanGovernanceService>? logger = null)
    {
        _planGovernanceRepository = planGovernanceRepository;
        _providerCreditRepository = providerCreditRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _userRepository = userRepository;
        _logger = logger ?? NullLogger<PlanGovernanceService>.Instance;
    }

    public async Task<AdminPlanGovernanceSnapshotDto> GetAdminSnapshotAsync(
        bool includeInactivePromotions = true,
        bool includeInactiveCoupons = true)
    {
        var nowUtc = DateTime.UtcNow;
        var users = (await _userRepository.GetAllAsync()).ToList();
        var planSettings = await BuildPlanSettingsSnapshotAsync(users);
        var promotions = await BuildPromotionsSnapshotAsync(includeInactivePromotions, nowUtc);
        var coupons = await BuildCouponsSnapshotAsync(includeInactiveCoupons, nowUtc);

        return new AdminPlanGovernanceSnapshotDto(
            planSettings,
            promotions,
            coupons);
    }

    public async Task<AdminOperationResultDto> UpdatePlanSettingAsync(
        ProviderPlan plan,
        AdminUpdatePlanSettingRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!ManagedPlans.Contains(plan))
        {
            return new AdminOperationResultDto(false, "invalid_plan", "Somente planos Bronze, Silver e Gold podem ser alterados.");
        }

        if (!TryNormalizePlanSettingRequest(
                request,
                out var monthlyPrice,
                out var maxRadiusKm,
                out var maxAllowedCategories,
                out var allowedCategories,
                out var validationError))
        {
            return new AdminOperationResultDto(false, "validation_error", validationError);
        }

        var setting = await _planGovernanceRepository.GetPlanSettingAsync(plan);
        var before = setting == null
            ? null
            : new
            {
                setting.MonthlyPrice,
                setting.MaxRadiusKm,
                setting.MaxAllowedCategories,
                allowedCategories = setting.AllowedCategories.Select(x => x.ToString()).OrderBy(x => x).ToArray()
            };

        if (setting == null)
        {
            setting = new ProviderPlanSetting
            {
                Plan = plan,
                MonthlyPrice = monthlyPrice,
                MaxRadiusKm = maxRadiusKm,
                MaxAllowedCategories = maxAllowedCategories,
                AllowedCategories = allowedCategories
            };

            await _planGovernanceRepository.AddPlanSettingAsync(setting);
        }
        else
        {
            setting.MonthlyPrice = monthlyPrice;
            setting.MaxRadiusKm = maxRadiusKm;
            setting.MaxAllowedCategories = maxAllowedCategories;
            setting.AllowedCategories = allowedCategories;
            setting.UpdatedAt = DateTime.UtcNow;

            await _planGovernanceRepository.UpdatePlanSettingAsync(setting);
        }

        await WriteAuditAsync(actorUserId, actorEmail, "ProviderPlanSettingUpdated", setting.Id, new
        {
            plan = plan.ToString(),
            before,
            after = new
            {
                setting.MonthlyPrice,
                setting.MaxRadiusKm,
                setting.MaxAllowedCategories,
                allowedCategories = setting.AllowedCategories.Select(x => x.ToString()).OrderBy(x => x).ToArray()
            }
        });

        _logger.LogInformation(
            "Provider plan setting updated. ActorUserId={ActorUserId}, Plan={Plan}",
            actorUserId,
            plan);

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminOperationResultDto> CreatePromotionAsync(
        AdminCreatePlanPromotionRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!TryNormalizePromotionRequest(
                request.Plan,
                request.Name,
                request.DiscountType,
                request.DiscountValue,
                request.StartsAtUtc,
                request.EndsAtUtc,
                out var normalizedName,
                out var startsAtUtc,
                out var endsAtUtc,
                out var validationError))
        {
            return new AdminOperationResultDto(false, "validation_error", validationError);
        }

        var promotion = new ProviderPlanPromotion
        {
            Plan = request.Plan,
            Name = normalizedName,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            IsActive = true
        };

        await _planGovernanceRepository.AddPromotionAsync(promotion);
        await WriteAuditAsync(actorUserId, actorEmail, "ProviderPlanPromotionCreated", promotion.Id, new
        {
            plan = promotion.Plan.ToString(),
            promotion.Name,
            promotion.DiscountType,
            promotion.DiscountValue,
            promotion.StartsAtUtc,
            promotion.EndsAtUtc,
            promotion.IsActive
        });

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminOperationResultDto> UpdatePromotionAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var promotion = await _planGovernanceRepository.GetPromotionByIdAsync(promotionId);
        if (promotion == null)
        {
            return new AdminOperationResultDto(false, "not_found", "Promocao nao encontrada.");
        }

        if (!TryNormalizePromotionRequest(
                promotion.Plan,
                request.Name,
                request.DiscountType,
                request.DiscountValue,
                request.StartsAtUtc,
                request.EndsAtUtc,
                out var normalizedName,
                out var startsAtUtc,
                out var endsAtUtc,
                out var validationError))
        {
            return new AdminOperationResultDto(false, "validation_error", validationError);
        }

        var before = new
        {
            promotion.Name,
            promotion.DiscountType,
            promotion.DiscountValue,
            promotion.StartsAtUtc,
            promotion.EndsAtUtc,
            promotion.IsActive
        };

        promotion.Name = normalizedName;
        promotion.DiscountType = request.DiscountType;
        promotion.DiscountValue = request.DiscountValue;
        promotion.StartsAtUtc = startsAtUtc;
        promotion.EndsAtUtc = endsAtUtc;
        promotion.UpdatedAt = DateTime.UtcNow;
        await _planGovernanceRepository.UpdatePromotionAsync(promotion);

        await WriteAuditAsync(actorUserId, actorEmail, "ProviderPlanPromotionUpdated", promotion.Id, new
        {
            plan = promotion.Plan.ToString(),
            before,
            after = new
            {
                promotion.Name,
                promotion.DiscountType,
                promotion.DiscountValue,
                promotion.StartsAtUtc,
                promotion.EndsAtUtc,
                promotion.IsActive
            }
        });

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminOperationResultDto> UpdatePromotionStatusAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionStatusRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var promotion = await _planGovernanceRepository.GetPromotionByIdAsync(promotionId);
        if (promotion == null)
        {
            return new AdminOperationResultDto(false, "not_found", "Promocao nao encontrada.");
        }

        if (promotion.IsActive == request.IsActive)
        {
            return new AdminOperationResultDto(true);
        }

        var before = new { promotion.IsActive };
        promotion.IsActive = request.IsActive;
        promotion.UpdatedAt = DateTime.UtcNow;
        await _planGovernanceRepository.UpdatePromotionAsync(promotion);

        await WriteAuditAsync(actorUserId, actorEmail, "ProviderPlanPromotionStatusChanged", promotion.Id, new
        {
            plan = promotion.Plan.ToString(),
            before,
            after = new { promotion.IsActive },
            reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim()
        });

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminOperationResultDto> CreateCouponAsync(
        AdminCreatePlanCouponRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!TryNormalizeCouponRequest(
                request.Code,
                request.Name,
                request.Plan,
                request.DiscountType,
                request.DiscountValue,
                request.StartsAtUtc,
                request.EndsAtUtc,
                request.MaxGlobalUses,
                request.MaxUsesPerProvider,
                out var normalizedCode,
                out var normalizedName,
                out var startsAtUtc,
                out var endsAtUtc,
                out var validationError))
        {
            return new AdminOperationResultDto(false, "validation_error", validationError);
        }

        var existing = await _planGovernanceRepository.GetCouponByCodeAsync(normalizedCode);
        if (existing != null)
        {
            return new AdminOperationResultDto(false, "duplicate_code", "Ja existe um cupom com esse codigo.");
        }

        var coupon = new ProviderPlanCoupon
        {
            Code = normalizedCode,
            Name = normalizedName,
            Plan = request.Plan,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            MaxGlobalUses = request.MaxGlobalUses,
            MaxUsesPerProvider = request.MaxUsesPerProvider,
            IsActive = true
        };

        await _planGovernanceRepository.AddCouponAsync(coupon);
        await WriteAuditAsync(actorUserId, actorEmail, "ProviderPlanCouponCreated", coupon.Id, new
        {
            coupon.Code,
            coupon.Name,
            plan = coupon.Plan?.ToString(),
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.StartsAtUtc,
            coupon.EndsAtUtc,
            coupon.MaxGlobalUses,
            coupon.MaxUsesPerProvider,
            coupon.IsActive
        });

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminOperationResultDto> UpdateCouponAsync(
        Guid couponId,
        AdminUpdatePlanCouponRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var coupon = await _planGovernanceRepository.GetCouponByIdAsync(couponId);
        if (coupon == null)
        {
            return new AdminOperationResultDto(false, "not_found", "Cupom nao encontrado.");
        }

        if (!TryNormalizeCouponRequest(
                coupon.Code,
                request.Name,
                request.Plan,
                request.DiscountType,
                request.DiscountValue,
                request.StartsAtUtc,
                request.EndsAtUtc,
                request.MaxGlobalUses,
                request.MaxUsesPerProvider,
                out _,
                out var normalizedName,
                out var startsAtUtc,
                out var endsAtUtc,
                out var validationError))
        {
            return new AdminOperationResultDto(false, "validation_error", validationError);
        }

        var before = new
        {
            coupon.Name,
            plan = coupon.Plan?.ToString(),
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.StartsAtUtc,
            coupon.EndsAtUtc,
            coupon.MaxGlobalUses,
            coupon.MaxUsesPerProvider,
            coupon.IsActive
        };

        coupon.Name = normalizedName;
        coupon.Plan = request.Plan;
        coupon.DiscountType = request.DiscountType;
        coupon.DiscountValue = request.DiscountValue;
        coupon.StartsAtUtc = startsAtUtc;
        coupon.EndsAtUtc = endsAtUtc;
        coupon.MaxGlobalUses = request.MaxGlobalUses;
        coupon.MaxUsesPerProvider = request.MaxUsesPerProvider;
        coupon.UpdatedAt = DateTime.UtcNow;

        await _planGovernanceRepository.UpdateCouponAsync(coupon);
        await WriteAuditAsync(actorUserId, actorEmail, "ProviderPlanCouponUpdated", coupon.Id, new
        {
            code = coupon.Code,
            before,
            after = new
            {
                coupon.Name,
                plan = coupon.Plan?.ToString(),
                coupon.DiscountType,
                coupon.DiscountValue,
                coupon.StartsAtUtc,
                coupon.EndsAtUtc,
                coupon.MaxGlobalUses,
                coupon.MaxUsesPerProvider,
                coupon.IsActive
            }
        });

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminOperationResultDto> UpdateCouponStatusAsync(
        Guid couponId,
        AdminUpdatePlanCouponStatusRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var coupon = await _planGovernanceRepository.GetCouponByIdAsync(couponId);
        if (coupon == null)
        {
            return new AdminOperationResultDto(false, "not_found", "Cupom nao encontrado.");
        }

        if (coupon.IsActive == request.IsActive)
        {
            return new AdminOperationResultDto(true);
        }

        var before = new { coupon.IsActive };
        coupon.IsActive = request.IsActive;
        coupon.UpdatedAt = DateTime.UtcNow;
        await _planGovernanceRepository.UpdateCouponAsync(coupon);

        await WriteAuditAsync(actorUserId, actorEmail, "ProviderPlanCouponStatusChanged", coupon.Id, new
        {
            code = coupon.Code,
            before,
            after = new { coupon.IsActive },
            reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim()
        });

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminPlanPriceSimulationResultDto> SimulatePriceAsync(AdminPlanPriceSimulationRequestDto request)
    {
        if (!ManagedPlans.Contains(request.Plan))
        {
            return new AdminPlanPriceSimulationResultDto(
                false,
                0m,
                0m,
                0m,
                0m,
                null,
                null,
                ErrorCode: "invalid_plan",
                ErrorMessage: "Plano invalido para simulacao.");
        }

        if (request.ConsumeCredits && !request.ProviderUserId.HasValue)
        {
            return new AdminPlanPriceSimulationResultDto(
                false,
                0m,
                0m,
                0m,
                0m,
                null,
                null,
                ErrorCode: "provider_required_for_credit_consumption",
                ErrorMessage: "Informe ProviderUserId para consumir credito na simulacao.");
        }

        var atUtc = request.AtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        if (request.ProviderUserId.HasValue)
        {
            var provider = await _userRepository.GetByIdAsync(request.ProviderUserId.Value);
            if (provider == null || provider.Role != UserRole.Provider)
            {
                return new AdminPlanPriceSimulationResultDto(
                    false,
                    0m,
                    0m,
                    0m,
                    0m,
                    null,
                    null,
                    ErrorCode: "provider_not_found",
                    ErrorMessage: "Prestador nao encontrado para simulacao de mensalidade.");
            }
        }

        var rules = await GetEffectivePlanSettingAsync(request.Plan);
        var basePrice = rules.MonthlyPrice;

        var promotions = await _planGovernanceRepository.GetPromotionsAsync(includeInactive: false);
        var activePromotion = promotions
            .Where(x => x.Plan == request.Plan && x.IsActive && x.StartsAtUtc <= atUtc && x.EndsAtUtc >= atUtc)
            .Select(x => new
            {
                Promotion = x,
                DiscountAmount = ComputeDiscount(basePrice, x.DiscountType, x.DiscountValue)
            })
            .OrderByDescending(x => x.DiscountAmount)
            .ThenByDescending(x => x.Promotion.StartsAtUtc)
            .FirstOrDefault();

        var promotionDiscount = activePromotion?.DiscountAmount ?? 0m;
        var priceAfterPromotion = Math.Max(0m, basePrice - promotionDiscount);

        decimal couponDiscount = 0m;
        string? couponCodeApplied = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var normalizedCouponCode = NormalizeCouponCode(request.CouponCode);
            if (string.IsNullOrWhiteSpace(normalizedCouponCode))
            {
                return new AdminPlanPriceSimulationResultDto(
                    false,
                    basePrice,
                    promotionDiscount,
                    0m,
                    priceAfterPromotion,
                    activePromotion?.Promotion.Name,
                    null,
                    ErrorCode: "invalid_coupon",
                    ErrorMessage: "Cupom invalido.");
            }

            var coupon = await _planGovernanceRepository.GetCouponByCodeAsync(normalizedCouponCode);
            if (coupon == null || !coupon.IsActive)
            {
                return new AdminPlanPriceSimulationResultDto(
                    false,
                    basePrice,
                    promotionDiscount,
                    0m,
                    priceAfterPromotion,
                    activePromotion?.Promotion.Name,
                    null,
                    ErrorCode: "coupon_not_found",
                    ErrorMessage: "Cupom nao encontrado ou inativo.");
            }

            if (coupon.StartsAtUtc > atUtc || coupon.EndsAtUtc < atUtc)
            {
                return new AdminPlanPriceSimulationResultDto(
                    false,
                    basePrice,
                    promotionDiscount,
                    0m,
                    priceAfterPromotion,
                    activePromotion?.Promotion.Name,
                    null,
                    ErrorCode: "coupon_out_of_date",
                    ErrorMessage: "Cupom fora da vigencia.");
            }

            if (coupon.Plan.HasValue && coupon.Plan.Value != request.Plan)
            {
                return new AdminPlanPriceSimulationResultDto(
                    false,
                    basePrice,
                    promotionDiscount,
                    0m,
                    priceAfterPromotion,
                    activePromotion?.Promotion.Name,
                    null,
                    ErrorCode: "coupon_plan_mismatch",
                    ErrorMessage: "Cupom nao pode ser aplicado para o plano selecionado.");
            }

            var globalUsage = await _planGovernanceRepository.GetCouponGlobalUsageCountAsync(coupon.Id);
            if (coupon.MaxGlobalUses.HasValue && globalUsage >= coupon.MaxGlobalUses.Value)
            {
                return new AdminPlanPriceSimulationResultDto(
                    false,
                    basePrice,
                    promotionDiscount,
                    0m,
                    priceAfterPromotion,
                    activePromotion?.Promotion.Name,
                    null,
                    ErrorCode: "coupon_global_limit",
                    ErrorMessage: "Cupom atingiu o limite global de uso.");
            }

            if (request.ProviderUserId.HasValue && coupon.MaxUsesPerProvider.HasValue)
            {
                var providerUsage = await _planGovernanceRepository.GetCouponUsageCountByProviderAsync(coupon.Id, request.ProviderUserId.Value);
                if (providerUsage >= coupon.MaxUsesPerProvider.Value)
                {
                    return new AdminPlanPriceSimulationResultDto(
                        false,
                        basePrice,
                        promotionDiscount,
                        0m,
                        priceAfterPromotion,
                        activePromotion?.Promotion.Name,
                        null,
                        ErrorCode: "coupon_provider_limit",
                        ErrorMessage: "Cupom atingiu o limite por prestador.");
                }
            }

            couponDiscount = ComputeDiscount(priceAfterPromotion, coupon.DiscountType, coupon.DiscountValue);
            couponCodeApplied = coupon.Code;
        }

        var priceBeforeCredits = Math.Max(0m, priceAfterPromotion - couponDiscount);
        var availableCredits = 0m;
        var creditsApplied = 0m;
        var creditsRemaining = 0m;
        var creditsConsumed = false;
        Guid? creditsConsumptionEntryId = null;

        if (request.ProviderUserId.HasValue)
        {
            await ExpireCreditsIfNeededAsync(request.ProviderUserId.Value, atUtc);
            var wallet = await _providerCreditRepository.GetWalletAsync(request.ProviderUserId.Value);
            availableCredits = decimal.Round(Math.Max(0m, wallet?.CurrentBalance ?? 0m), 2, MidpointRounding.AwayFromZero);
            creditsApplied = decimal.Round(Math.Min(priceBeforeCredits, availableCredits), 2, MidpointRounding.AwayFromZero);
            creditsRemaining = decimal.Round(Math.Max(0m, availableCredits - creditsApplied), 2, MidpointRounding.AwayFromZero);

            if (request.ConsumeCredits && priceBeforeCredits > 0m && availableCredits > 0m)
            {
                var consumedEntry = await _providerCreditRepository.AppendEntryAsync(
                    request.ProviderUserId.Value,
                    currentWallet =>
                    {
                        var amountToConsume = decimal.Round(
                            Math.Min(priceBeforeCredits, Math.Max(0m, currentWallet.CurrentBalance)),
                            2,
                            MidpointRounding.AwayFromZero);

                        if (amountToConsume <= 0m)
                        {
                            throw new InvalidOperationException("Sem saldo de credito para consumo.");
                        }

                        return new ProviderCreditLedgerEntry
                        {
                            ProviderId = request.ProviderUserId.Value,
                            EntryType = ProviderCreditLedgerEntryType.Debit,
                            Amount = amountToConsume,
                            BalanceBefore = currentWallet.CurrentBalance,
                            BalanceAfter = currentWallet.CurrentBalance - amountToConsume,
                            Reason = $"Abatimento da mensalidade simulada do plano {request.Plan.ToPtBr()}",
                            Source = "monthly_subscription_simulation",
                            ReferenceType = "plan_governance_simulation",
                            EffectiveAtUtc = atUtc,
                            Metadata = JsonSerializer.Serialize(new
                            {
                                request.Plan,
                                request.CouponCode,
                                basePrice,
                                promotionDiscount,
                                couponDiscount,
                                priceBeforeCredits
                            })
                        };
                    });

                availableCredits = decimal.Round(consumedEntry.BalanceBefore, 2, MidpointRounding.AwayFromZero);
                creditsApplied = decimal.Round(consumedEntry.Amount, 2, MidpointRounding.AwayFromZero);
                creditsRemaining = decimal.Round(consumedEntry.BalanceAfter, 2, MidpointRounding.AwayFromZero);
                creditsConsumed = true;
                creditsConsumptionEntryId = consumedEntry.Id;
            }
        }

        var finalPrice = decimal.Round(Math.Max(0m, priceBeforeCredits - creditsApplied), 2, MidpointRounding.AwayFromZero);
        return new AdminPlanPriceSimulationResultDto(
            true,
            basePrice,
            promotionDiscount,
            couponDiscount,
            finalPrice,
            activePromotion?.Promotion.Name,
            couponCodeApplied,
            priceBeforeCredits,
            availableCredits,
            creditsApplied,
            creditsRemaining,
            creditsConsumed,
            creditsConsumptionEntryId);
    }

    public async Task<IReadOnlyList<ProviderPlanOfferDto>> GetProviderPlanOffersAsync(DateTime? atUtc = null)
    {
        var nowUtc = atUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var promotions = await _planGovernanceRepository.GetPromotionsAsync(includeInactive: false);
        var settings = await _planGovernanceRepository.GetPlanSettingsAsync();
        var byPlan = settings.ToDictionary(x => x.Plan, x => x);

        return ManagedPlans
            .Select(plan =>
            {
                var setting = byPlan.TryGetValue(plan, out var existing)
                    ? existing
                    : BuildDefaultPlanSetting(plan);

                var promotion = promotions
                    .Where(x => x.Plan == plan && x.IsActive && x.StartsAtUtc <= nowUtc && x.EndsAtUtc >= nowUtc)
                    .Select(x => new { Entity = x, Discount = ComputeDiscount(setting.MonthlyPrice, x.DiscountType, x.DiscountValue) })
                    .OrderByDescending(x => x.Discount)
                    .ThenByDescending(x => x.Entity.StartsAtUtc)
                    .FirstOrDefault();

                var promotionDiscount = promotion?.Discount ?? 0m;
                var finalPrice = Math.Max(0m, setting.MonthlyPrice - promotionDiscount);
                return new ProviderPlanOfferDto(
                    plan,
                    plan.ToPtBr(),
                    setting.MonthlyPrice,
                    promotionDiscount,
                    finalPrice,
                    promotion?.Entity.EndsAtUtc);
            })
            .OrderBy(x => x.Plan)
            .ToList();
    }

    public async Task<ProviderOperationalPlanRulesDto?> GetOperationalRulesAsync(ProviderPlan plan)
    {
        if (!ManagedPlans.Contains(plan))
        {
            return null;
        }

        var setting = await GetEffectivePlanSettingAsync(plan);
        return new ProviderOperationalPlanRulesDto(
            plan,
            setting.MaxRadiusKm,
            setting.MaxAllowedCategories,
            setting.AllowedCategories);
    }

    public async Task<ProviderOperationalValidationResultDto> ValidateOperationalSelectionAsync(
        ProviderPlan plan,
        double radiusKm,
        IReadOnlyCollection<ServiceCategory> categories)
    {
        var rules = await GetOperationalRulesAsync(plan);
        if (rules == null)
        {
            return new ProviderOperationalValidationResultDto(false, "rules_not_found", "Plano sem regras operacionais configuradas.");
        }

        if (radiusKm <= 0 || radiusKm > rules.MaxRadiusKm)
        {
            return new ProviderOperationalValidationResultDto(
                false,
                "radius_limit_exceeded",
                $"O raio maximo permitido para o plano {rules.Plan.ToPtBr()} e {rules.MaxRadiusKm:0.#} km.");
        }

        if (categories == null || categories.Count == 0)
        {
            return new ProviderOperationalValidationResultDto(false, "missing_categories", "Selecione ao menos uma categoria.");
        }

        if (categories.Count > rules.MaxAllowedCategories)
        {
            return new ProviderOperationalValidationResultDto(
                false,
                "max_categories_exceeded",
                $"O plano {rules.Plan.ToPtBr()} permite no maximo {rules.MaxAllowedCategories} categoria(s).");
        }

        var notAllowed = categories
            .Where(c => !rules.AllowedCategories.Contains(c))
            .Distinct()
            .ToList();
        if (notAllowed.Count > 0)
        {
            return new ProviderOperationalValidationResultDto(
                false,
                "category_not_allowed",
                $"Categoria(s) nao permitida(s) para o plano {rules.Plan.ToPtBr()}: {string.Join(", ", notAllowed.Select(x => x.ToPtBr()))}.");
        }

        return new ProviderOperationalValidationResultDto(true);
    }

    private async Task<IReadOnlyList<AdminPlanSettingDto>> BuildPlanSettingsSnapshotAsync(IReadOnlyList<User> users)
    {
        var settings = await _planGovernanceRepository.GetPlanSettingsAsync();
        var byPlan = settings.ToDictionary(x => x.Plan, x => x);

        var result = new List<AdminPlanSettingDto>(ManagedPlans.Count);
        foreach (var plan in ManagedPlans)
        {
            var setting = byPlan.TryGetValue(plan, out var existing)
                ? existing
                : BuildDefaultPlanSetting(plan);

            var providersOutsideLimits = users.Count(u =>
                u.Role == UserRole.Provider &&
                u.IsActive &&
                u.ProviderProfile != null &&
                u.ProviderProfile.Plan == plan &&
                !IsProfileCompliant(u.ProviderProfile, setting));

            result.Add(new AdminPlanSettingDto(
                setting.Plan,
                setting.Plan.ToPtBr(),
                setting.MonthlyPrice,
                setting.MaxRadiusKm,
                setting.MaxAllowedCategories,
                setting.AllowedCategories.Select(x => x.ToPtBr()).ToList(),
                providersOutsideLimits,
                setting.CreatedAt,
                setting.UpdatedAt));
        }

        return result;
    }

    private async Task<IReadOnlyList<AdminPlanPromotionDto>> BuildPromotionsSnapshotAsync(bool includeInactive, DateTime atUtc)
    {
        var promotions = await _planGovernanceRepository.GetPromotionsAsync(includeInactive);
        return promotions
            .Select(p => new AdminPlanPromotionDto(
                p.Id,
                p.Plan,
                p.Plan.ToPtBr(),
                p.Name,
                p.DiscountType,
                p.DiscountValue,
                p.StartsAtUtc,
                p.EndsAtUtc,
                p.IsActive,
                p.IsActive && p.StartsAtUtc <= atUtc && p.EndsAtUtc >= atUtc,
                p.CreatedAt,
                p.UpdatedAt))
            .OrderByDescending(x => x.IsCurrentlyActive)
            .ThenByDescending(x => x.StartsAtUtc)
            .ToList();
    }

    private async Task<IReadOnlyList<AdminPlanCouponDto>> BuildCouponsSnapshotAsync(bool includeInactive, DateTime atUtc)
    {
        var coupons = await _planGovernanceRepository.GetCouponsAsync(includeInactive);
        return coupons
            .Select(c => new AdminPlanCouponDto(
                c.Id,
                c.Code,
                c.Name,
                c.Plan,
                c.Plan?.ToPtBr(),
                c.DiscountType,
                c.DiscountValue,
                c.StartsAtUtc,
                c.EndsAtUtc,
                c.MaxGlobalUses,
                c.MaxUsesPerProvider,
                c.Redemptions.Count,
                c.IsActive,
                c.IsActive && c.StartsAtUtc <= atUtc && c.EndsAtUtc >= atUtc,
                c.CreatedAt,
                c.UpdatedAt))
            .OrderByDescending(x => x.IsCurrentlyActive)
            .ThenBy(x => x.Code)
            .ToList();
    }

    private async Task<ProviderPlanSetting> GetEffectivePlanSettingAsync(ProviderPlan plan)
    {
        var existing = await _planGovernanceRepository.GetPlanSettingAsync(plan);
        return existing ?? BuildDefaultPlanSetting(plan);
    }

    private static ProviderPlanSetting BuildDefaultPlanSetting(ProviderPlan plan)
    {
        return plan switch
        {
            ProviderPlan.Bronze => new ProviderPlanSetting
            {
                Plan = plan,
                MonthlyPrice = ProviderSubscriptionPricingCatalog.GetMonthlyPrice(plan),
                MaxRadiusKm = 25,
                MaxAllowedCategories = 3,
                AllowedCategories = AllCategories.ToList()
            },
            ProviderPlan.Silver => new ProviderPlanSetting
            {
                Plan = plan,
                MonthlyPrice = ProviderSubscriptionPricingCatalog.GetMonthlyPrice(plan),
                MaxRadiusKm = 40,
                MaxAllowedCategories = 5,
                AllowedCategories = AllCategories.ToList()
            },
            ProviderPlan.Gold => new ProviderPlanSetting
            {
                Plan = plan,
                MonthlyPrice = ProviderSubscriptionPricingCatalog.GetMonthlyPrice(plan),
                MaxRadiusKm = 60,
                MaxAllowedCategories = AllCategories.Count,
                AllowedCategories = AllCategories.ToList()
            },
            _ => new ProviderPlanSetting
            {
                Plan = plan,
                MonthlyPrice = ProviderSubscriptionPricingCatalog.GetMonthlyPrice(plan),
                MaxRadiusKm = 10,
                MaxAllowedCategories = 1,
                AllowedCategories = new List<ServiceCategory> { ServiceCategory.Other }
            }
        };
    }

    private static decimal ComputeDiscount(decimal baseAmount, PricingDiscountType discountType, decimal discountValue)
    {
        if (baseAmount <= 0 || discountValue <= 0)
        {
            return 0m;
        }

        var discount = discountType == PricingDiscountType.Percentage
            ? baseAmount * (discountValue / 100m)
            : discountValue;

        return Math.Max(0m, Math.Min(baseAmount, Math.Round(discount, 2, MidpointRounding.AwayFromZero)));
    }

    private static bool TryNormalizePlanSettingRequest(
        AdminUpdatePlanSettingRequestDto request,
        out decimal monthlyPrice,
        out double maxRadiusKm,
        out int maxAllowedCategories,
        out List<ServiceCategory> allowedCategories,
        out string validationError)
    {
        monthlyPrice = request.MonthlyPrice;
        maxRadiusKm = request.MaxRadiusKm;
        maxAllowedCategories = request.MaxAllowedCategories;
        allowedCategories = new List<ServiceCategory>();
        validationError = string.Empty;

        if (monthlyPrice < 0)
        {
            validationError = "Preco mensal deve ser maior ou igual a zero.";
            return false;
        }

        if (maxRadiusKm <= 0 || maxRadiusKm > 500)
        {
            validationError = "Raio maximo deve estar entre 1 e 500 km.";
            return false;
        }

        if (maxAllowedCategories <= 0 || maxAllowedCategories > 20)
        {
            validationError = "Quantidade maxima de categorias deve estar entre 1 e 20.";
            return false;
        }

        if (request.AllowedCategories == null || request.AllowedCategories.Count == 0)
        {
            validationError = "Informe ao menos uma categoria permitida para o plano.";
            return false;
        }

        allowedCategories = request.AllowedCategories
            .Select(item =>
            {
                if (ServiceCategoryExtensions.TryParseFlexible(item, out var category))
                {
                    return (valid: true, category);
                }

                return (valid: false, category: default(ServiceCategory));
            })
            .Where(x => x.valid)
            .Select(x => x.category)
            .Distinct()
            .OrderBy(x => (int)x)
            .ToList();

        if (allowedCategories.Count == 0)
        {
            validationError = "Categorias permitidas invalidas.";
            return false;
        }

        if (maxAllowedCategories > allowedCategories.Count)
        {
            validationError = "Limite de categorias nao pode ser maior que a quantidade de categorias permitidas.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizePromotionRequest(
        ProviderPlan plan,
        string? name,
        PricingDiscountType discountType,
        decimal discountValue,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        out string normalizedName,
        out DateTime normalizedStartsAtUtc,
        out DateTime normalizedEndsAtUtc,
        out string validationError)
    {
        normalizedName = (name ?? string.Empty).Trim();
        normalizedStartsAtUtc = startsAtUtc.ToUniversalTime();
        normalizedEndsAtUtc = endsAtUtc.ToUniversalTime();
        validationError = string.Empty;

        if (!ManagedPlans.Contains(plan))
        {
            validationError = "Plano invalido para promocao.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            validationError = "Nome da promocao e obrigatorio.";
            return false;
        }

        if (normalizedName.Length > 140)
        {
            validationError = "Nome da promocao deve ter no maximo 140 caracteres.";
            return false;
        }

        if (!IsValidDiscount(discountType, discountValue, out validationError))
        {
            return false;
        }

        if (normalizedStartsAtUtc >= normalizedEndsAtUtc)
        {
            validationError = "Data de inicio deve ser menor que a data de fim.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeCouponRequest(
        string? code,
        string? name,
        ProviderPlan? plan,
        PricingDiscountType discountType,
        decimal discountValue,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int? maxGlobalUses,
        int? maxUsesPerProvider,
        out string normalizedCode,
        out string normalizedName,
        out DateTime normalizedStartsAtUtc,
        out DateTime normalizedEndsAtUtc,
        out string validationError)
    {
        normalizedCode = NormalizeCouponCode(code);
        normalizedName = (name ?? string.Empty).Trim();
        normalizedStartsAtUtc = startsAtUtc.ToUniversalTime();
        normalizedEndsAtUtc = endsAtUtc.ToUniversalTime();
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            validationError = "Codigo do cupom e obrigatorio.";
            return false;
        }

        if (normalizedCode.Length > 40)
        {
            validationError = "Codigo do cupom deve ter no maximo 40 caracteres.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            validationError = "Nome do cupom e obrigatorio.";
            return false;
        }

        if (normalizedName.Length > 120)
        {
            validationError = "Nome do cupom deve ter no maximo 120 caracteres.";
            return false;
        }

        if (plan.HasValue && !ManagedPlans.Contains(plan.Value))
        {
            validationError = "Plano alvo do cupom invalido.";
            return false;
        }

        if (!IsValidDiscount(discountType, discountValue, out validationError))
        {
            return false;
        }

        if (normalizedStartsAtUtc >= normalizedEndsAtUtc)
        {
            validationError = "Data de inicio deve ser menor que a data de fim.";
            return false;
        }

        if (maxGlobalUses.HasValue && maxGlobalUses.Value <= 0)
        {
            validationError = "Limite global de uso deve ser maior que zero.";
            return false;
        }

        if (maxUsesPerProvider.HasValue && maxUsesPerProvider.Value <= 0)
        {
            validationError = "Limite por prestador deve ser maior que zero.";
            return false;
        }

        return true;
    }

    private static bool IsValidDiscount(PricingDiscountType discountType, decimal discountValue, out string validationError)
    {
        validationError = string.Empty;
        if (discountValue <= 0)
        {
            validationError = "Valor de desconto deve ser maior que zero.";
            return false;
        }

        if (discountType == PricingDiscountType.Percentage && discountValue > 100)
        {
            validationError = "Desconto percentual nao pode ser maior que 100%.";
            return false;
        }

        return true;
    }

    private static bool IsProfileCompliant(ProviderProfile profile, ProviderPlanSetting setting)
    {
        if (profile.RadiusKm <= 0 || profile.RadiusKm > setting.MaxRadiusKm)
        {
            return false;
        }

        var categories = profile.Categories ?? new List<ServiceCategory>();
        if (categories.Count == 0 || categories.Count > setting.MaxAllowedCategories)
        {
            return false;
        }

        return categories.All(c => setting.AllowedCategories.Contains(c));
    }

    private static string NormalizeCouponCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var normalized = code
            .Trim()
            .ToUpperInvariant();

        var chars = normalized
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray();
        return new string(chars);
    }

    private async Task ExpireCreditsIfNeededAsync(Guid providerId, DateTime atUtc)
    {
        var entries = await _providerCreditRepository.GetEntriesChronologicalAsync(providerId);
        if (entries.Count == 0)
        {
            return;
        }

        var lots = new List<CreditLot>();
        foreach (var entry in entries)
        {
            switch (entry.EntryType)
            {
                case ProviderCreditLedgerEntryType.Grant:
                case ProviderCreditLedgerEntryType.Reversal:
                    if (entry.Amount > 0)
                    {
                        lots.Add(new CreditLot(entry.EffectiveAtUtc, entry.ExpiresAtUtc, entry.Amount));
                    }

                    break;

                case ProviderCreditLedgerEntryType.Debit:
                case ProviderCreditLedgerEntryType.Expire:
                    ConsumeLots(lots, entry.Amount);
                    break;
            }
        }

        var expiredLots = lots
            .Where(x => x.RemainingAmount > 0 && x.ExpiresAtUtc.HasValue && x.ExpiresAtUtc.Value <= atUtc)
            .ToList();
        if (expiredLots.Count == 0)
        {
            return;
        }

        var expiredAmount = decimal.Round(
            expiredLots.Sum(x => x.RemainingAmount),
            2,
            MidpointRounding.AwayFromZero);
        if (expiredAmount <= 0m)
        {
            return;
        }

        try
        {
            await _providerCreditRepository.AppendEntryAsync(
                providerId,
                wallet =>
                {
                    var amountToExpire = decimal.Round(
                        Math.Min(expiredAmount, Math.Max(0m, wallet.CurrentBalance)),
                        2,
                        MidpointRounding.AwayFromZero);

                    if (amountToExpire <= 0m)
                    {
                        throw new InvalidOperationException("Sem saldo de credito para expiracao automatica.");
                    }

                    return new ProviderCreditLedgerEntry
                    {
                        ProviderId = providerId,
                        EntryType = ProviderCreditLedgerEntryType.Expire,
                        Amount = amountToExpire,
                        BalanceBefore = wallet.CurrentBalance,
                        BalanceAfter = wallet.CurrentBalance - amountToExpire,
                        Reason = "Expiracao automatica de creditos vencidos",
                        Source = "credit_expiration_job",
                        ReferenceType = "credit_expiration",
                        EffectiveAtUtc = atUtc,
                        Metadata = JsonSerializer.Serialize(new
                        {
                            expiredLots = expiredLots.Count,
                            expiredAmount
                        })
                    };
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Automatic credit expiration skipped due to invalid operation. ProviderId={ProviderId}",
                providerId);
        }
    }

    private static void ConsumeLots(List<CreditLot> lots, decimal amount)
    {
        var remainingAmount = decimal.Round(Math.Max(0m, amount), 2, MidpointRounding.AwayFromZero);
        if (remainingAmount <= 0m)
        {
            return;
        }

        foreach (var lot in lots
                     .Where(x => x.RemainingAmount > 0)
                     .OrderBy(x => x.EffectiveAtUtc))
        {
            if (remainingAmount <= 0m)
            {
                break;
            }

            var consumed = decimal.Round(
                Math.Min(lot.RemainingAmount, remainingAmount),
                2,
                MidpointRounding.AwayFromZero);

            lot.RemainingAmount -= consumed;
            remainingAmount -= consumed;
        }
    }

    private sealed class CreditLot
    {
        public CreditLot(DateTime effectiveAtUtc, DateTime? expiresAtUtc, decimal amount)
        {
            EffectiveAtUtc = effectiveAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            RemainingAmount = decimal.Round(Math.Max(0m, amount), 2, MidpointRounding.AwayFromZero);
        }

        public DateTime EffectiveAtUtc { get; }
        public DateTime? ExpiresAtUtc { get; }
        public decimal RemainingAmount { get; set; }
    }

    private async Task WriteAuditAsync(Guid actorUserId, string actorEmail, string action, Guid targetId, object metadata)
    {
        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = action,
            TargetType = "ProviderPlanGovernance",
            TargetId = targetId,
            Metadata = JsonSerializer.Serialize(metadata)
        });
    }
}
