using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if !IOS && !ANDROID
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
#endif
using localllama.Models;

namespace localllama.Services
{
    // Versão limpa e funcional do serviço de geração.
    // Usa AiDocumentStructureService para chamar o modelo LLM e obter a estrutura JSON.
    // Em caso de falha do modelo, usa o parser heurístico como fallback.
    public class DocumentGenerationService2
    {
        private readonly AiDocumentStructureService _aiStructure = new AiDocumentStructureService();

        /// <summary>
        /// Gera o ficheiro final e devolve o caminho absoluto do ficheiro temporário criado.
        /// </summary>
        /// <param name="request">Pedido de geração com texto, formato e extensão.</param>
        /// <param name="onStatusUpdate">Callback opcional para actualizar o estado da UI.</param>
        public async Task<string> GenerateToPathAsync(
            DocumentGenerationRequest request,
            Action<string>? onStatusUpdate = null)
        {
            if (string.IsNullOrWhiteSpace(request.RawText))
                throw new ArgumentException("Conteúdo vazio");

            // --- 1. Obter estrutura intermédia via IA (com fallback heurístico) ---
            var structure = await _aiStructure.ParseWithAiAsync(
                request.RawText,
                request.OutputFormat,
                request.CodeExtension,
                onStatusUpdate);

            var tmpDir = FileSystem.CacheDirectory;
            Directory.CreateDirectory(tmpDir);

            // --- 2. Gerar o ficheiro no formato escolhido ---
            if (request.OutputFormat.StartsWith("Word", StringComparison.OrdinalIgnoreCase))
            {
                onStatusUpdate?.Invoke("A gerar ficheiro Word (.docx)...");
                var path = Path.Combine(tmpDir, (SanitizeFileName(structure.Title) ?? "document") + ".docx");
                CreateDocx(structure, path);
                return path;
            }

            if (request.OutputFormat.StartsWith("PDF", StringComparison.OrdinalIgnoreCase))
            {
                onStatusUpdate?.Invoke("A gerar ficheiro PDF...");
                var path = Path.Combine(tmpDir, (SanitizeFileName(structure.Title) ?? "document") + ".pdf");
                CreatePdf(structure, path);
                return path;
            }

            if (request.OutputFormat.StartsWith("Código", StringComparison.OrdinalIgnoreCase))
            {
                onStatusUpdate?.Invoke("A gerar ficheiro de código...");
                var ext = request.CodeExtension ?? ".txt";
                var fileName = (SanitizeFileName(structure.Title) ?? "code") + ext;
                var path = Path.Combine(tmpDir, fileName);
                CreateCodeFile(structure, path, ext);
                return path;
            }

            throw new NotSupportedException("Formato de saída não suportado.");
        }

        private static string? SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

#if !IOS && !ANDROID
        private static void CreateDocx(DocumentStructure doc, string path)
        {
            if (File.Exists(path)) File.Delete(path);

            using var word = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
            var mainPart = word.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            if (!string.IsNullOrWhiteSpace(doc.Title))
            {
                var titlePara = new Paragraph(new Run(new Text(doc.Title)))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new SpacingBetweenLines { After = "240" })
                };
                body.AppendChild(titlePara);
            }

            foreach (var section in doc.Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Heading))
                {
                    var heading = new Paragraph(new Run(new Text(section.Heading)))
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new SpacingBetweenLines { Before = "240", After = "120" })
                    };
                    // Apply Heading1 style if it exists — silently skip if not
                    heading.ParagraphProperties.ParagraphStyleId = new ParagraphStyleId { Val = "Heading1" };
                    body.AppendChild(heading);
                }

                foreach (var para in section.Paragraphs)
                {
                    var p = new Paragraph(new Run(new Text(para)))
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new SpacingBetweenLines { After = "120" })
                    };
                    body.AppendChild(p);
                }

                foreach (var list in section.Lists)
                {
                    foreach (var item in list)
                    {
                        var p = new Paragraph(new Run(new Text("• " + item)));
                        body.AppendChild(p);
                    }
                    body.AppendChild(new Paragraph(new Run(new Text(string.Empty))));
                }

                foreach (var cb in section.CodeBlocks)
                {
                    // Add a simple code-formatted paragraph (monospace via font hint)
                    var run = new Run(new Text(cb.Code ?? string.Empty));
                    run.RunProperties = new RunProperties(new RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" });
                    var p = new Paragraph(run);
                    body.AppendChild(p);
                }

                body.AppendChild(new Paragraph(new Run(new Text(string.Empty))));
            }

            mainPart.Document.Save();
        }

        private static void CreatePdf(DocumentStructure doc, string path)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var pdf = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().PaddingBottom(12).Text(t =>
                    {
                        t.Span(doc.Title ?? string.Empty).SemiBold().FontSize(20);
                    });

                    page.Content().Column(col =>
                    {
                        foreach (var section in doc.Sections)
                        {
                            if (!string.IsNullOrWhiteSpace(section.Heading))
                            {
                                col.Item()
                                   .PaddingTop(8)
                                   .Text(section.Heading)
                                   .Bold()
                                   .FontSize(14);
                            }

                            foreach (var para in section.Paragraphs)
                            {
                                col.Item()
                                   .PaddingTop(4)
                                   .Text(para)
                                   .FontSize(11);
                            }

                            foreach (var list in section.Lists)
                            {
                                foreach (var item in list)
                                {
                                    col.Item()
                                       .PaddingLeft(12)
                                       .Text("• " + item)
                                       .FontSize(11);
                                }
                                col.Item().PaddingTop(4);
                            }

                            foreach (var cb in section.CodeBlocks)
                            {
                                col.Item()
                                   .Background("#F4F4F4")
                                   .Padding(8)
                                   .Text(cb.Code ?? string.Empty)
                                   .FontFamily("Courier New")
                                   .FontSize(9);
                            }

                            col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor("#CCCCCC");
                        }
                    });

                    page.Footer()
                        .AlignRight()
                        .Text(x =>
                        {
                            x.Span("Gerado por LocalLlama · ").FontSize(8).FontColor("#999999");
                            x.CurrentPageNumber().FontSize(8).FontColor("#999999");
                        });
                });
            });

            pdf.GeneratePdf(path);
        }
#else
        // Fallbacks for platforms where OpenXml/QuestPDF may not be available (iOS/Android).
        private static void CreateDocx(DocumentStructure doc, string path)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(doc.Title)) sb.AppendLine(doc.Title);
            sb.AppendLine();
            foreach (var s in doc.Sections)
            {
                if (!string.IsNullOrWhiteSpace(s.Heading)) sb.AppendLine($"## {s.Heading}");
                foreach (var p in s.Paragraphs) sb.AppendLine(p);
                foreach (var lb in s.Lists) foreach (var it in lb) sb.AppendLine("- " + it);
                foreach (var cb in s.CodeBlocks) sb.AppendLine(cb.Code ?? string.Empty);
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
        }

        private static void CreatePdf(DocumentStructure doc, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine(doc.Title ?? string.Empty);
            sb.AppendLine();
            foreach (var s in doc.Sections)
            {
                if (!string.IsNullOrWhiteSpace(s.Heading)) sb.AppendLine($"## {s.Heading}");
                foreach (var p in s.Paragraphs) sb.AppendLine(p);
                foreach (var lb in s.Lists) foreach (var it in lb) sb.AppendLine("- " + it);
                foreach (var cb in s.CodeBlocks) sb.AppendLine(cb.Code ?? string.Empty);
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
        }
#endif

        private static void CreateCodeFile(DocumentStructure doc, string path, string ext)
        {
            var sb = new StringBuilder();
            var codeBlocks = doc.Sections.SelectMany(s => s.CodeBlocks).ToList();

            if (codeBlocks.Any())
            {
                // Preferir blocos de código extraídos pelo LLM/parser
                foreach (var cb in codeBlocks)
                {
                    if (!string.IsNullOrWhiteSpace(cb.Code))
                        sb.AppendLine(cb.Code);
                }
            }
            else
            {
                // Sem blocos de código: comentar parágrafos como contexto
                var commentPrefix = ext switch
                {
                    ".py" => "# ",
                    ".rs" => "// ",
                    ".cs" => "// ",
                    _ => "// "
                };

                if (!string.IsNullOrWhiteSpace(doc.Title))
                    sb.AppendLine($"{commentPrefix}{doc.Title}");

                foreach (var s in doc.Sections)
                {
                    if (!string.IsNullOrWhiteSpace(s.Heading))
                        sb.AppendLine($"{commentPrefix}--- {s.Heading} ---");

                    foreach (var p in s.Paragraphs)
                        sb.AppendLine($"{commentPrefix}{p}");

                    foreach (var cb in s.CodeBlocks)
                        if (!string.IsNullOrWhiteSpace(cb.Code))
                            sb.AppendLine(cb.Code);
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}
