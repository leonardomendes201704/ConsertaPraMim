using AppMobileCPM.Models;

namespace AppMobileCPM.Services;

public interface IMarketplaceRepository
{
    IReadOnlyList<ServiceCategory> GetCategories();
    ServiceCategory? GetCategoryById(string categoryId);
    IReadOnlyList<string> GetProfessionOptions();
    IReadOnlyList<Professional> GetProfessionals(string? searchTerm = null);
    IReadOnlyList<string> GetSupportCategoryOptions();
    IReadOnlyList<FaqItem> GetSupportFaqItems();
    IReadOnlyDictionary<string, string> GetSiteContents();
    string? GetSiteContent(string key);
    void AddServiceRequest(ServiceRequest request);
    void AddProfessionalRegistration(ProfessionalRegistration registration);
    void AddSupportRequest(SupportRequest request);
}
