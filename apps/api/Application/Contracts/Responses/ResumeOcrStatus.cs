namespace JobPilot.Api.Application.Contracts;

public record ResumeOcrStatus(bool Available, ResumeOcrCommandStatus Tesseract, ResumeOcrCommandStatus OcrMyPdf, string[] Notes);
