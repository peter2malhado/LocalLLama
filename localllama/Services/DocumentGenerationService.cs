using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OpenXmlDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using OpenXmlParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using OpenXmlRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using OpenXmlText = DocumentFormat.OpenXml.Wordprocessing.Text;
using OpenXmlBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using OpenXmlParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using OpenXmlJustification = DocumentFormat.OpenXml.Wordprocessing.Justification;
using OpenXmlJustificationValues = DocumentFormat.OpenXml.Wordprocessing.JustificationValues;
using OpenXmlSpacingBetweenLines = DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines;
using OpenXmlParagraphStyleId = DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId;
using OpenXmlRunFonts = DocumentFormat.OpenXml.Wordprocessing.RunFonts;
using OpenXmlRunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using OpenXmlWordprocessingDocumentType = DocumentFormat.OpenXml.WordprocessingDocumentType;
using localllama.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace localllama.Services;

public class DocumentGenerationService
{
    private readonly AiDocumentStructureService _aiStructure;
    private readonly DocumentParseService _fallbackParser = new();
    private readonly GeneratedDocumentService _generatedDocumentService = new();

    public DocumentGenerationService(ChatInferenceService? inferenceService = null)
    {
        _aiStructure = new AiDocumentStructureService(inferenceService);
    }

    public async Task<DocumentGenerationResult> GenerateAsync(
        DocumentGenerationRequest request,
        Action<string>? onStatusUpdate = null)
    {
        ValidateRequest(request);

        var structure = await _aiStructure.ParseWithAiAsync(
            request.RawText,
            request.OutputFormat,
            request.CodeExtension,
            onStatusUpdate);

        var cacheDir = FileSystem.CacheDirectory;
        Directory.CreateDirectory(cacheDir);

        var result = request.OutputFormat switch
        {
            var format when IsWord(format) => CreateWordResult(structure, cacheDir, onStatusUpdate),
            var format when IsPdf(format) => CreatePdfResult(structure, cacheDir, onStatusUpdate),
            var format when IsCode(format) => CreateCodeResult(structure, cacheDir, request.CodeExtension, onStatusUpdate),
            _ => throw new NotSupportedException("Formato de saída não suportado.")
        };

        onStatusUpdate?.Invoke("A guardar no programa...");
        var stored = await _generatedDocumentService.SaveAsync(result.FilePath, result.FileName, result.MimeType);
        return result with { FilePath = stored.Path };
    }

    private static void ValidateRequest(DocumentGenerationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.RawText))
            throw new ArgumentException("Conteúdo vazio.");

        if (!IsWord(request.OutputFormat) && !IsPdf(request.OutputFormat) && !IsCode(request.OutputFormat))
            throw new NotSupportedException("Formato de saída não suportado.");

        if (IsCode(request.OutputFormat) && string.IsNullOrWhiteSpace(request.CodeExtension))
            throw new ArgumentException("Escolhe extensão de código.");
    }

    private static bool IsWord(string format) => format.StartsWith("Word", StringComparison.OrdinalIgnoreCase);
    private static bool IsPdf(string format) => format.StartsWith("PDF", StringComparison.OrdinalIgnoreCase);
    private static bool IsCode(string format) => format.StartsWith("Código", StringComparison.OrdinalIgnoreCase);

    private static DocumentGenerationResult CreateWordResult(
        DocumentStructure structure,
        string cacheDir,
        Action<string>? onStatusUpdate)
    {
        onStatusUpdate?.Invoke("A gerar DOCX...");
        var fileName = $"{SanitizeFileName(structure.Title) ?? "document"}.docx";
        var path = Path.Combine(cacheDir, fileName);
        WriteDocx(structure, path);
        return new DocumentGenerationResult(path, fileName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    private static DocumentGenerationResult CreatePdfResult(
        DocumentStructure structure,
        string cacheDir,
        Action<string>? onStatusUpdate)
    {
        onStatusUpdate?.Invoke("A gerar PDF...");
        var fileName = $"{SanitizeFileName(structure.Title) ?? "document"}.pdf";
        var path = Path.Combine(cacheDir, fileName);
        WritePdf(structure, path);
        return new DocumentGenerationResult(path, fileName, "application/pdf");
    }

    private static DocumentGenerationResult CreateCodeResult(
        DocumentStructure structure,
        string cacheDir,
        string? extension,
        Action<string>? onStatusUpdate)
    {
        onStatusUpdate?.Invoke("A gerar código...");
        var ext = NormalizeExtension(extension);
        var fileName = $"{SanitizeFileName(structure.Title) ?? "code"}{ext}";
        var path = Path.Combine(cacheDir, fileName);
        File.WriteAllText(path, BuildCodeText(structure), Encoding.UTF8);
        return new DocumentGenerationResult(path, fileName, GetMimeForExtension(ext));
    }

    private static string NormalizeExtension(string? extension)
        => string.IsNullOrWhiteSpace(extension) ? ".txt" : extension.StartsWith('.') ? extension : "." + extension;

    private static string BuildCodeText(DocumentStructure structure)
    {
        var sb = new StringBuilder();
        foreach (var section in structure.Sections)
        {
            foreach (var codeBlock in section.CodeBlocks)
            {
                if (!string.IsNullOrWhiteSpace(codeBlock.Code))
                {
                    sb.AppendLine(codeBlock.Code);
                    sb.AppendLine();
                }
            }
        }

        if (sb.Length == 0)
        {
            foreach (var section in structure.Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Heading))
                    sb.AppendLine($"// {section.Heading}");

                foreach (var paragraph in section.Paragraphs)
                    sb.AppendLine(section.Heading != null ? $"// {paragraph}" : paragraph);

                foreach (var list in section.Lists)
                    foreach (var item in list)
                        sb.AppendLine($"// - {item}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void WriteDocx(DocumentStructure doc, string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        using var word = WordprocessingDocument.Create(path, OpenXmlWordprocessingDocumentType.Document);
        var mainPart = word.AddMainDocumentPart();
        mainPart.Document = new OpenXmlDocument();
        var body = mainPart.Document.AppendChild(new OpenXmlBody());

        if (!string.IsNullOrWhiteSpace(doc.Title))
        {
            body.AppendChild(new OpenXmlParagraph(
                new OpenXmlRun(new OpenXmlText(doc.Title) { Space = SpaceProcessingModeValues.Preserve }))
            {
                ParagraphProperties = new OpenXmlParagraphProperties(
                    new OpenXmlJustification { Val = OpenXmlJustificationValues.Center },
                    new OpenXmlSpacingBetweenLines { After = "240" })
            });
        }

        foreach (var section in doc.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Heading))
            {
                body.AppendChild(new OpenXmlParagraph(
                    new OpenXmlRun(new OpenXmlText(section.Heading) { Space = SpaceProcessingModeValues.Preserve }))
                {
                    ParagraphProperties = new OpenXmlParagraphProperties(
                        new OpenXmlParagraphStyleId { Val = "Heading1" },
                        new OpenXmlSpacingBetweenLines { Before = "240", After = "120" })
                });
            }

            foreach (var paragraph in section.Paragraphs)
            {
                body.AppendChild(new OpenXmlParagraph(
                    new OpenXmlRun(new OpenXmlText(paragraph) { Space = SpaceProcessingModeValues.Preserve }))
                {
                    ParagraphProperties = new OpenXmlParagraphProperties(new OpenXmlSpacingBetweenLines { After = "120" })
                });
            }

            foreach (var list in section.Lists)
            {
                foreach (var item in list)
                    body.AppendChild(new OpenXmlParagraph(new OpenXmlRun(new OpenXmlText($"• {item}") { Space = SpaceProcessingModeValues.Preserve })));
            }

            foreach (var codeBlock in section.CodeBlocks)
            {
                var run = new OpenXmlRun(new OpenXmlText(codeBlock.Code ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
                run.RunProperties = new OpenXmlRunProperties(new OpenXmlRunFonts { Ascii = "Courier New", HighAnsi = "Courier New" });
                body.AppendChild(new OpenXmlParagraph(run));
            }

            body.AppendChild(new OpenXmlParagraph(new OpenXmlRun(new OpenXmlText(string.Empty))));
        }

        mainPart.Document.Save();
    }

    private static void WritePdf(DocumentStructure doc, string path)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Header().PaddingBottom(10).Text(doc.Title ?? string.Empty).SemiBold().FontSize(20);

                page.Content().Column(column =>
                {
                    foreach (var section in doc.Sections)
                    {
                        if (!string.IsNullOrWhiteSpace(section.Heading))
                            column.Item().PaddingTop(6).Text(section.Heading).Bold().FontSize(14);

                        foreach (var paragraph in section.Paragraphs)
                            column.Item().Text(paragraph);

                        foreach (var list in section.Lists)
                            foreach (var item in list)
                                column.Item().Text($"• {item}");

                        foreach (var codeBlock in section.CodeBlocks)
                            column.Item()
                                .Background("#F4F4F4")
                                .Padding(8)
                                .Text(codeBlock.Code ?? string.Empty)
                                .FontFamily("Courier New")
                                .FontSize(9);
                    }
                });
            });
        });

        pdf.GeneratePdf(path);
    }

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return name.Trim();
    }

    private static string GetMimeForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => "text/plain",
        ".py" => "text/x-python",
        ".rs" => "text/plain",
        _ => "application/octet-stream"
    };
}
