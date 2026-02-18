namespace ConsertaPraMim.Application.Interfaces;

public interface IRequestWarningCollector
{
    void AddWarning(string warningCode);
    IReadOnlyList<string> GetWarnings();
    void Clear();
}
