using System;
using System.Linq;
using localllama.Models;

namespace localllama.Services
{
    // Simples parser heurístico que gera a estrutura intermédia JSON esperada.
    public class DocumentParseService
    {
        public DocumentStructure Parse(string rawText, string? preferredLanguage = null)
        {
            var doc = new DocumentStructure
            {
                Title = ExtractTitle(rawText),
                Format = null,
                Language = preferredLanguage
            };

            var sections = rawText.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var sec in sections)
            {
                var s = sec.Trim();
                var section = new Section();

                // Heading: primeira linha se curta
                var lines = s.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && lines[0].Length < 80 && lines.Length > 1)
                {
                    section.Heading = lines[0].Trim();
                    var rest = string.Join('\n', lines.Skip(1)).Trim();
                    section.Paragraphs.AddRange(rest.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }
                else
                {
                    // Sem heading claro, tudo em parágrafos
                    section.Paragraphs.AddRange(lines.Select(l => l.Trim()));
                }

                // Detectar blocos de código com ```
                if (s.Contains("```"))
                {
                    var parts = s.Split(new[] { "```" }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (i % 2 == 1)
                        {
                            var codeText = parts[i].Trim();
                            var firstLine = codeText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                            var lang = !string.IsNullOrWhiteSpace(firstLine) && firstLine.Trim().All(c => char.IsLetter(c)) && firstLine.Trim().Length <= 8
                                ? firstLine.Trim()
                                : preferredLanguage;
                            if (lang != null && codeText.StartsWith(lang))
                            {
                                codeText = string.Join('\n', codeText.Split('\n').Skip(1));
                            }

                            section.CodeBlocks.Add(new CodeBlock { Language = lang, Code = codeText });
                        }
                    }
                }

                doc.Sections.Add(section);
            }

            if (!doc.Sections.Any())
                doc.Sections.Add(new Section { Paragraphs = { rawText } });

            return doc;
        }

        private string? ExtractTitle(string raw)
        {
            var lines = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
            if (!lines.Any()) return null;
            var first = lines[0];
            if (first.Length < 80 && first.Contains(' ')) return first;
            return null;
        }
    }
}
