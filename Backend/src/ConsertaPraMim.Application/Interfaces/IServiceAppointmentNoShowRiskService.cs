namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceAppointmentNoShowRiskService
{
    Task<int> EvaluateNoShowRiskAsync(int batchSize = 200, CancellationToken cancellationToken = default);
}
