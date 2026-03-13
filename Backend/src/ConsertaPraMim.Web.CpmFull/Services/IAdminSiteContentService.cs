namespace AppMobileCPM.Services;

public interface IAdminSiteContentService
{
    IReadOnlyList<AdminSiteContentRecord> GetAll();
    AdminSiteContentRecord? GetById(int id);
    void Create(AdminSiteContentUpsertRequest request);
    bool Update(int id, AdminSiteContentUpsertRequest request);
    bool Delete(int id);
}
