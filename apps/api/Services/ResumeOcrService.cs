using System.Diagnostics;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public sealed class ResumeOcrService
{
    public ResumeOcrStatus GetStatus()
    {
        var tesseract = FindCommand("tesseract");
        var ocrMyPdf = FindCommand("ocrmypdf");
        var available = tesseract.Available || ocrMyPdf.Available;
        var notes = new List<string>();

        if (!tesseract.Available)
            notes.Add("Tesseract nao encontrado no PATH. OCR local de imagem/PDF escaneado ficara indisponivel.");
        if (!ocrMyPdf.Available)
            notes.Add("OCRmyPDF nao encontrado no PATH. Criacao automatica de PDF pesquisavel ficara indisponivel.");

        return new ResumeOcrStatus(available, tesseract, ocrMyPdf, notes.ToArray());
    }

    public ResumeOcrResult RunOcr(string filePath)
    {
        var status = GetStatus();
        if (!status.Available)
            return new ResumeOcrResult("", 0, ["OCR nao disponivel. Instale Tesseract ou OCRmyPDF."], false);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".pdf" && status.OcrMyPdf.Available)
            return RunOcrMyPdf(filePath);
        if (status.Tesseract.Available)
            return RunTesseract(filePath);

        return new ResumeOcrResult("", 0, ["Nenhuma ferramenta de OCR disponivel para este arquivo."], false);
    }

    private static ResumeOcrResult RunTesseract(string filePath)
    {
        try
        {
            var outputBase = Path.Combine(Path.GetTempPath(), $"jobpilot-ocr-{Guid.NewGuid():N}");
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "tesseract",
                ArgumentList = { filePath, outputBase, "-l", "por+eng" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            var txtPath = $"{outputBase}.txt";
            if (process.ExitCode == 0 && File.Exists(txtPath))
            {
                var text = File.ReadAllText(txtPath);
                File.Delete(txtPath);
                var wordCount = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                return new ResumeOcrResult(text, wordCount, [], true);
            }

            return new ResumeOcrResult("", 0, [$"Tesseract falhou: {error}"], false);
        }
        catch (Exception ex)
        {
            return new ResumeOcrResult("", 0, [$"Erro ao executar Tesseract: {ex.Message}"], false);
        }
    }

    private static ResumeOcrResult RunOcrMyPdf(string filePath)
    {
        try
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"jobpilot-ocr-{Guid.NewGuid():N}.pdf");
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "ocrmypdf",
                ArgumentList = { "--force-ocr", "--deskew", filePath, outputPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(60000);

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                // Extract text from the OCRed PDF using PdfPig
                var text = ExtractTextFromPdf(outputPath);
                File.Delete(outputPath);
                var wordCount = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                return new ResumeOcrResult(text, wordCount, [], true);
            }

            return new ResumeOcrResult("", 0, [$"OCRmyPDF falhou: {error}"], false);
        }
        catch (Exception ex)
        {
            return new ResumeOcrResult("", 0, [$"Erro ao executar OCRmyPDF: {ex.Message}"], false);
        }
    }

    private static string ExtractTextFromPdf(string path)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            using var document = UglyToad.PdfPig.PdfDocument.Open(path);
            foreach (var page in document.GetPages())
                sb.AppendLine(page.Text ?? "");
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static ResumeOcrCommandStatus FindCommand(string command)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where.exe" : "which",
                ArgumentList = { command },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);
            var path = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return new ResumeOcrCommandStatus(command, process.ExitCode == 0 && !string.IsNullOrWhiteSpace(path), path);
        }
        catch
        {
            return new ResumeOcrCommandStatus(command, false, "");
        }
    }
}

public sealed record ResumeOcrResult(string Text, int WordCount, string[] Warnings, bool Success);
