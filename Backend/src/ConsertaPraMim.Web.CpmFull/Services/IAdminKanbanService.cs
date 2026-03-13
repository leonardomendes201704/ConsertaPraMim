namespace AppMobileCPM.Services;

public interface IAdminKanbanService
{
    AdminKanbanBoardData GetBoard(string boardType);
    IReadOnlyList<AdminKanbanStageRecord> GetStages(string boardType);
    AdminKanbanLeadDetailsRecord? GetLeadDetails(int leadId);
    int CreateLead(AdminKanbanLeadUpsertRequest request);
    bool UpdateLead(int leadId, AdminKanbanLeadUpsertRequest request);
    bool SaveBoardOrder(AdminKanbanBoardOrderUpdateRequest request);
    bool AddHistoryNote(int leadId, string note);
}
