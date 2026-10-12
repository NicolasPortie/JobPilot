using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public sealed class JobTargetSourceFileService(IWebHostEnvironment environment)
{
    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    public async Task<SavedJobTargetSourceFile> SaveAsync(Guid userId, IFormFile file)
    {
        if (file is null)
            throw new InvalidOperationException("Envie uma imagem ou PDF da vaga.");
        if (file.Length <= 0)
            throw new InvalidOperationException("Arquivo vazio.");
        if (file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Arquivo muito grande. Envie imagem ou PDF de até 10 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.TryGetValue(extension, out var mediaType))
            throw new InvalidOperationException("Formato inválido. Use PNG, JPG, WEBP ou PDF.");

        await UploadFilePolicy.ValidateSignatureAsync(file, extension);

        var root = string.IsNullOrWhiteSpace(environment.ContentRootPath)
            ? Directory.GetCurrentDirectory()
            : environment.ContentRootPath;
        var directory = Path.Combine(root, "storage", "job-target-sources", userId.ToString("N"));
        Directory.CreateDirectory(directory);

        var safeName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(directory, safeName);
        try
        {
            await using var stream = File.Create(path);
            await file.CopyToAsync(stream);
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }

        try
        {
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                UploadFilePolicy.ValidatePdfReadable(path);
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }

        return new SavedJobTargetSourceFile(path, mediaType, file.FileName);
    }

    public static async Task<AiInputAttachment> ToAttachmentAsync(SavedJobTargetSourceFile saved)
    {
        var bytes = await File.ReadAllBytesAsync(saved.Path);
        var dataUrl = $"data:{saved.MediaType};base64,{Convert.ToBase64String(bytes)}";
        var type = saved.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ? "file" : "image_url";
        return new AiInputAttachment(type, saved.MediaType, dataUrl, saved.OriginalFileName);
    }
}

public record SavedJobTargetSourceFile(string Path, string MediaType, string OriginalFileName);
