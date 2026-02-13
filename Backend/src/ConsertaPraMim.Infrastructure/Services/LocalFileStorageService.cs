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
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));

        var webRootPath = GetWebRootPath();
        string uploadsFolder = Path.Combine(webRootPath, "uploads", folder);
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

        var webRootPath = GetWebRootPath();
        string fullPath = Path.Combine(webRootPath, filePath.TrimStart('/'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private string GetWebRootPath()
    {
        var webRootPath = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(_env.ContentRootPath, "wwwroot");
        }

        if (!Directory.Exists(webRootPath))
        {
            Directory.CreateDirectory(webRootPath);
        }

        return webRootPath;
    }
}
