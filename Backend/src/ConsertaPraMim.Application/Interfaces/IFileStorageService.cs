
namespace ConsertaPraMim.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder);
    void DeleteFile(string filePath);
}
