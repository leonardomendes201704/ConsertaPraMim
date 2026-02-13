using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class ProviderOnboardingService : IProviderOnboardingService
{
    private static readonly HashSet<ProviderPlan> AllowedOnboardingPlans = new()
    {
        ProviderPlan.Bronze,
        ProviderPlan.Silver,
        ProviderPlan.Gold
    };

    private static readonly HashSet<ProviderDocumentType> RequiredDocumentTypes = new()
    {
        ProviderDocumentType.IdentityDocument,
        ProviderDocumentType.SelfieWithDocument
    };

    private readonly IUserRepository _userRepository;
    private readonly IPlanGovernanceService _planGovernanceService;

    public ProviderOnboardingService(
        IUserRepository userRepository,
        IPlanGovernanceService planGovernanceService)
    {
        _userRepository = userRepository;
        _planGovernanceService = planGovernanceService;
    }

    public async Task<ProviderOnboardingStateDto?> GetStateAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider)
        {
            return null;
        }

        var profile = user.ProviderProfile ?? CreateLegacyCompletedProfile(userId);
        if (user.ProviderProfile == null)
        {
            user.ProviderProfile = profile;
            await _userRepository.UpdateAsync(user);
        }

        var offers = await _planGovernanceService.GetProviderPlanOffersAsync();
        return MapState(user, profile, offers);
    }

    public async Task<bool> SaveBasicDataAsync(Guid userId, UpdateProviderOnboardingBasicDataDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            return false;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider)
        {
            return false;
        }

        var profile = EnsureOnboardingProfile(user);
        user.Name = dto.Name.Trim();
        user.Phone = dto.Phone.Trim();
        profile.OnboardingStartedAt ??= DateTime.UtcNow;
        profile.OnboardingStatus = profile.IsOnboardingCompleted ? ProviderOnboardingStatus.Active : ProviderOnboardingStatus.PendingDocumentation;

        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<bool> SavePlanAsync(Guid userId, ProviderPlan plan)
    {
        if (!AllowedOnboardingPlans.Contains(plan))
        {
            return false;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider)
        {
            return false;
        }

        var profile = EnsureOnboardingProfile(user);
        profile.Plan = plan;
        profile.PlanSelectedAt = DateTime.UtcNow;
        profile.OnboardingStartedAt ??= DateTime.UtcNow;
        if (!profile.IsOnboardingCompleted)
        {
            profile.OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation;
        }

        var validation = await _planGovernanceService.ValidateOperationalSelectionAsync(
            plan,
            profile.RadiusKm,
            profile.Categories);
        if (validation.Success)
        {
            profile.HasOperationalCompliancePending = false;
            profile.OperationalComplianceNotes = null;
        }
        else
        {
            profile.HasOperationalCompliancePending = true;
            profile.OperationalComplianceNotes = validation.ErrorMessage;
        }

        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<ProviderOnboardingDocumentDto?> AddDocumentAsync(Guid userId, AddProviderOnboardingDocumentDto dto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider)
        {
            return null;
        }

        var profile = EnsureOnboardingProfile(user);
        if (profile.OnboardingDocuments.Count >= 6)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var document = new ProviderOnboardingDocument
        {
            ProviderProfileId = profile.Id,
            DocumentType = dto.DocumentType,
            Status = ProviderDocumentStatus.Pending,
            FileName = dto.FileName,
            MimeType = dto.MimeType,
            SizeBytes = dto.SizeBytes,
            FileUrl = dto.FileUrl,
            FileHashSha256 = dto.FileHashSha256,
            CreatedAt = now
        };

        profile.OnboardingDocuments.Add(document);
        profile.OnboardingStartedAt ??= now;
        profile.DocumentsSubmittedAt = now;
        if (!profile.IsOnboardingCompleted)
        {
            profile.OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation;
        }

        await _userRepository.UpdateAsync(user);
        return MapDocument(document);
    }

    public async Task<(bool Success, string? FileUrl)> RemoveDocumentAsync(Guid userId, Guid documentId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider || user.ProviderProfile == null)
        {
            return (false, null);
        }

        var document = user.ProviderProfile.OnboardingDocuments.FirstOrDefault(x => x.Id == documentId);
        if (document == null)
        {
            return (false, null);
        }

        var fileUrl = document.FileUrl;
        user.ProviderProfile.OnboardingDocuments.Remove(document);
        await _userRepository.UpdateAsync(user);
        return (true, fileUrl);
    }

    public async Task<CompleteProviderOnboardingResult> CompleteAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider)
        {
            return new CompleteProviderOnboardingResult(false, "Prestador nao encontrado.");
        }

        var profile = EnsureOnboardingProfile(user);

        if (!IsBasicDataCompleted(user))
        {
            return new CompleteProviderOnboardingResult(false, "Preencha os dados basicos antes de concluir.");
        }

        if (!IsPlanCompleted(profile))
        {
            return new CompleteProviderOnboardingResult(false, "Selecione um dos 3 planos para continuar.");
        }

        if (!HasRequiredDocuments(profile.OnboardingDocuments))
        {
            return new CompleteProviderOnboardingResult(false, "Envie os documentos obrigatorios para concluir.");
        }

        var now = DateTime.UtcNow;
        profile.DocumentsSubmittedAt ??= now;
        profile.OnboardingCompletedAt = now;
        profile.IsOnboardingCompleted = true;
        profile.OnboardingStatus = ProviderOnboardingStatus.PendingApproval;

        await _userRepository.UpdateAsync(user);
        return new CompleteProviderOnboardingResult(true, null);
    }

    public async Task<bool> IsOnboardingCompleteAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider)
        {
            return false;
        }

        if (user.ProviderProfile == null)
        {
            return true;
        }

        return user.ProviderProfile.IsOnboardingCompleted || user.ProviderProfile.OnboardingStatus == ProviderOnboardingStatus.Active;
    }

    private static ProviderProfile EnsureOnboardingProfile(User user)
    {
        if (user.ProviderProfile != null)
        {
            return user.ProviderProfile;
        }

        user.ProviderProfile = new ProviderProfile
        {
            UserId = user.Id,
            IsOnboardingCompleted = false,
            OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
            Plan = ProviderPlan.Trial,
            OnboardingStartedAt = DateTime.UtcNow,
            HasOperationalCompliancePending = false
        };

        return user.ProviderProfile;
    }

    private static ProviderProfile CreateLegacyCompletedProfile(Guid userId)
    {
        return new ProviderProfile
        {
            UserId = userId,
            Plan = ProviderPlan.Trial,
            IsOnboardingCompleted = true,
            OnboardingStatus = ProviderOnboardingStatus.Active,
            HasOperationalCompliancePending = false
        };
    }

    private static ProviderOnboardingStateDto MapState(
        User user,
        ProviderProfile profile,
        IReadOnlyList<ProviderPlanOfferDto> offers)
    {
        var docs = profile.OnboardingDocuments
            .OrderByDescending(d => d.CreatedAt)
            .Select(MapDocument)
            .ToList();

        var basicDataCompleted = IsBasicDataCompleted(user);
        var planCompleted = IsPlanCompleted(profile);
        var documentsCompleted = HasRequiredDocuments(profile.OnboardingDocuments);

        return new ProviderOnboardingStateDto(
            user.Name,
            user.Email,
            user.Phone,
            profile.Plan,
            profile.OnboardingStatus,
            basicDataCompleted,
            planCompleted,
            documentsCompleted,
            profile.IsOnboardingCompleted,
            profile.OnboardingStartedAt,
            profile.PlanSelectedAt,
            profile.DocumentsSubmittedAt,
            profile.OnboardingCompletedAt,
            docs,
            offers,
            profile.HasOperationalCompliancePending,
            profile.OperationalComplianceNotes);
    }

    private static ProviderOnboardingDocumentDto MapDocument(ProviderOnboardingDocument document)
    {
        return new ProviderOnboardingDocumentDto(
            document.Id,
            document.DocumentType,
            document.Status,
            document.FileName,
            document.MimeType,
            document.SizeBytes,
            document.FileUrl,
            document.CreatedAt,
            document.RejectionReason);
    }

    private static bool IsBasicDataCompleted(User user)
    {
        return !string.IsNullOrWhiteSpace(user.Name) && !string.IsNullOrWhiteSpace(user.Phone);
    }

    private static bool IsPlanCompleted(ProviderProfile profile)
    {
        return AllowedOnboardingPlans.Contains(profile.Plan) && profile.PlanSelectedAt.HasValue;
    }

    private static bool HasRequiredDocuments(IEnumerable<ProviderOnboardingDocument> documents)
    {
        var documentTypes = documents.Select(d => d.DocumentType).ToHashSet();
        return RequiredDocumentTypes.All(documentTypes.Contains);
    }
}
