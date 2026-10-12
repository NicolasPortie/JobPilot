using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;

namespace JobPilot.Api.Services;

public static class UploadFilePolicy
{
    private const int MaxExpandedArchiveBytes = 64 * 1024 * 1024;
    private const int MaxArchiveEntries = 256;
    private const int MaxPdfPages = 40;

    public static async Task ValidateSignatureAsync(IFormFile file, string extension)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory());

        switch (extension.ToLowerInvariant())
        {
            case ".pdf" when !StartsWith(header, read, "%PDF-"u8.ToArray()):
                throw new InvalidOperationException("O arquivo não corresponde a um PDF válido.");
            case ".png" when !StartsWith(header, read, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]):
                throw new InvalidOperationException("O arquivo não corresponde a uma imagem PNG válida.");
            case ".jpg" or ".jpeg" when read < 3 || header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF:
                throw new InvalidOperationException("O arquivo não corresponde a uma imagem JPEG válida.");
            case ".webp" when !StartsWith(header, read, "RIFF"u8.ToArray()) || !StartsWith(header[8..], read - 8, "WEBP"u8.ToArray()):
                throw new InvalidOperationException("O arquivo não corresponde a uma imagem WEBP válida.");
            case ".docx" when read < 4 || header[0] != 0x50 || header[1] != 0x4B || header[2] != 0x03 || header[3] != 0x04:
                throw new InvalidOperationException("O arquivo não corresponde a um DOCX válido.");
            case ".txt" or ".md" when header[..read].Contains((byte)0):
                throw new InvalidOperationException("O arquivo de texto contém dados binários inválidos.");
        }
    }

    public static async Task ValidateStoredArchiveAsync(string path, string extension)
    {
        if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await using var stream = File.OpenRead(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entries = 0;
            long expandedBytes = 0;
            var hasDocument = false;

            foreach (var entry in archive.Entries)
            {
                entries++;
                if (entries > MaxArchiveEntries)
                    throw new InvalidOperationException("O DOCX contém entradas demais para ser processado com segurança.");

                expandedBytes += entry.Length;
                if (expandedBytes > MaxExpandedArchiveBytes)
                    throw new InvalidOperationException("O DOCX excede o limite de expansão permitido.");

                var name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith("word/", StringComparison.OrdinalIgnoreCase) &&
                    (name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith("vbaData.xml", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("O DOCX contém macros, que não são aceitas por segurança.");

                if (entry.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                    hasDocument = true;
            }

            if (!hasDocument)
                throw new InvalidOperationException("O DOCX não contém um documento principal válido.");

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    continue;
                using var reader = new StreamReader(entry.Open());
                var rels = await reader.ReadToEndAsync();
                if (rels.Contains("TargetMode=\"External\"", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("O DOCX contém links externos, que não são aceitos por segurança.");
            }
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException("O arquivo DOCX está corrompido ou não é um pacote Office válido.", ex);
        }
    }

    private static bool StartsWith(byte[] value, int length, byte[] prefix) =>
        length >= prefix.Length && value.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    public static void ValidatePdfReadable(string path)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            var pages = 0;
            foreach (var _ in document.GetPages())
            {
                pages++;
                if (pages > MaxPdfPages)
                    throw new InvalidOperationException($"O PDF excede o limite de {MaxPdfPages} páginas.");
            }
            if (pages == 0)
                throw new InvalidOperationException("O PDF não contém páginas legíveis.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("ncrypt", StringComparison.OrdinalIgnoreCase) ||
                                   (ex.Message?.Contains("encrypt", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            throw new InvalidOperationException("O PDF está criptografado ou protegido. Envie uma versão sem senha.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("O PDF está corrompido ou ilegível. Verifique o arquivo e tente novamente.", ex);
        }
    }
}
