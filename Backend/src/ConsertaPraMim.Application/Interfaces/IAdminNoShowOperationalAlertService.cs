namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminNoShowOperationalAlertService
{
    Task<int> EvaluateAndNotifyAsync(CancellationToken cancellationToken = default);
}
