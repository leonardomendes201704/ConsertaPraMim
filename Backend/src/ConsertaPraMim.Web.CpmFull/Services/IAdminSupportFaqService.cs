namespace AppMobileCPM.Services;

public interface IAdminSupportFaqService
{
    IReadOnlyList<AdminSupportFaqRecord> GetAll();
    AdminSupportFaqRecord? GetById(int id);
    void Create(AdminSupportFaqUpsertRequest request);
    bool Update(int id, AdminSupportFaqUpsertRequest request);
    bool Delete(int id);
}
