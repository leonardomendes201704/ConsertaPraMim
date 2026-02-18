using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Infrastructure.Services;

public class RequestWarningCollector : IRequestWarningCollector
{
    private readonly List<string> _warnings = [];

    public void AddWarning(string warningCode)
    {
        if (string.IsNullOrWhiteSpace(warningCode))
        {
            return;
        }

        var normalized = warningCode.Trim();
        if (_warnings.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _warnings.Add(normalized);
    }

    public IReadOnlyList<string> GetWarnings()
    {
        return _warnings;
    }

    public void Clear()
    {
        _warnings.Clear();
    }
}
