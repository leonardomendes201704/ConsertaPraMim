namespace AppMobileCPM.Services;

public interface ISiteContentResolver
{
    IReadOnlyDictionary<string, string> GetAll();
    string Get(string key, string fallbackValue);
}
