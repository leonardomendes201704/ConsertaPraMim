using ConsertaPraMim.Application.Interfaces;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace ConsertaPraMim.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string folder)
    {
        if (fileStream == null) return null!;

        string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", folder);
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(originalFileName);
        string filePath = Path.Combine(uploadsFolder, fileName);

        using (var outputStream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(outputStream);
        }

        return $"/uploads/{folder}/{fileName}";
    }

    public void DeleteFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        string fullPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
