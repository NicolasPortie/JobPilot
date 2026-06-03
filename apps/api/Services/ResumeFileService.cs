using System.Text;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public sealed class ResumeFileService(IConfiguration configuration, ProfileImportService importer)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".md"
    };

    public async Task<ProfileImportResponse> ImportUploadAsync(Guid userId, IFormFile file, bool fetchPublicLinks, bool useAi)
    {
        if (file.Length <= 0) throw new InvalidOperationException("Arquivo vazio.");
        if (file.Length > 10 * 1024 * 1024) throw new InvalidOperationException("Arquivo maior que 10MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Formato nao suportado. Use PDF, DOCX, TXT ou MD.");

        var uploadRoot = configuration["Candidate:ResumeUploadDirectory"];
        if (string.IsNullOrWhiteSpace(uploadRoot))
            uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "documents", "resumes");

        var userDir = Path.Combine(uploadRoot, userId.ToString("N"));
        Directory.CreateDirectory(userDir);

        var safeBaseName = MakeSafeFileName(Path.GetFileNameWithoutExtension(file.FileName));
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{safeBaseName}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(userDir, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return await importer.ImportAsync(userId, new ProfileImportRequest(fullPath, fetchPublicLinks, useAi));
    }

    private static string MakeSafeFileName(string value)
    {
        var chars = value
            .Normalize(NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var collapsed = System.Text.RegularExpressions.Regex.Replace(new string(chars), "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "curriculo" : collapsed[..Math.Min(collapsed.Length, 80)];
    }
}
