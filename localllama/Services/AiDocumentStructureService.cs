using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using localllama.Models;
using System.Linq;

namespace localllama.Services
{
    /// <summary>
    /// Usa o modelo LLM local para converter texto em bruto numa estrutura JSON intermédia
    /// (DocumentStructure). Se o modelo não estiver disponível ou não gerar JSON válido,
    /// cai de volta para o parser heurístico.
    /// </summary>
    public class AiDocumentStructureService
    {
        private const string SystemPrompt =
            """
            És um assistente especializado em estruturar e reescrever documentos.
            Quando receberes texto, devolves APENAS um objeto JSON com a seguinte estrutura, sem qualquer explicação, comentário ou texto fora do JSON:
            {
              "title": "...",
              "format": "...",
              "language": "...",
              "sections": [
                {
                  "heading": "...",
                  "paragraphs": ["..."],
                  "lists": [["item1", "item2"]],
                  "codeBlocks": [{"language": "...", "code": "..."}]
                }
              ]
            }
            Regras:
            - "title": título principal do documento (string ou null).
            - "format": formato de saída solicitado ("docx", "pdf", "code") ou null.
            - "language": linguagem de programação se for código ("cs", "py", "rs") ou null.
            - "sections": lista de secções. Cada secção tem heading (string ou null), paragraphs (lista de strings), lists (lista de listas de strings), codeBlocks (lista de objetos com "language" e "code").
            - Agrupa o conteúdo em secções lógicas.
            - Reescreve notas soltas em texto mais natural, completo e útil.
            - Expande frases curtas quando isso melhora clareza, sem inventar factos.
            - Dá tom profissional, fluido e menos seco.
            - Blocos de código delimitados por ``` devem ir para "codeBlocks".
            - Listas com traço ou número devem ir para "lists".
            - Texto normal vai para "paragraphs".
            - Responde APENAS com o JSON, sem mais nada.
            """;

        private readonly DocumentParseService _fallbackParser = new DocumentParseService();
        private readonly ChatInferenceService? _sharedInferenceService;

        public AiDocumentStructureService(ChatInferenceService? sharedInferenceService = null)
        {
            _sharedInferenceService = sharedInferenceService;
        }

        /// <summary>
        /// Pede ao LLM que analise o texto e devolva JSON estruturado.
        /// Se o LLM falhar ou não tiver modelo carregado, usa o parser heurístico.
        /// </summary>
        public async Task<DocumentStructure> ParseWithAiAsync(
            string rawText,
            string outputFormat,
            string? codeExtension,
            Action<string>? onStatusUpdate = null)
        {
            try
            {
                onStatusUpdate?.Invoke("A inicializar modelo de IA...");

                var inferenceService = _sharedInferenceService ?? new ChatInferenceService();
                if (_sharedInferenceService == null)
                    inferenceService.Initialize();

                var userPrompt = BuildUserPrompt(rawText, outputFormat, codeExtension);

                // Rebuild session with the document structuring system prompt
                inferenceService.RebuildSession(Array.Empty<ChatMessage>(), SystemPrompt);

                onStatusUpdate?.Invoke("A gerar estrutura com IA...");

                var sb = new StringBuilder();
                var result = await inferenceService.GenerateReplyAsync(userPrompt, partial =>
                {
                    sb.Clear();
                    sb.Append(partial);
                });

                var jsonText = ExtractJson(result.FinalText);
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    var structure = TryDeserialize(jsonText, codeExtension);
                    if (structure != null)
                    {
                        onStatusUpdate?.Invoke("Estrutura gerada com IA com sucesso.");
                        return structure;
                    }
                }

                onStatusUpdate?.Invoke("JSON inválido — a usar parser heurístico...");
            }
            catch (Exception ex)
            {
                // Log silencioso; fallback abaixo
                System.Diagnostics.Debug.WriteLine($"[AiDocumentStructureService] Erro IA: {ex.Message}");
                onStatusUpdate?.Invoke($"IA indisponível ({ex.GetType().Name}) — a usar parser heurístico...");
            }

            // Fallback
            return _fallbackParser.Parse(rawText, preferredLanguage: codeExtension);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static string BuildUserPrompt(string rawText, string outputFormat, string? codeExt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Transforma o seguinte texto num documento bem escrito, útil e natural.");
            sb.AppendLine($"Formato de saída desejado: {outputFormat}");
            if (!string.IsNullOrWhiteSpace(codeExt))
                sb.AppendLine($"Linguagem de programação alvo: {codeExt}");
            sb.AppendLine("Mantém conteúdo relevante, melhora fluidez e completa frases curtas quando fizer sentido.");
            sb.AppendLine();
            sb.AppendLine("Texto:");
            sb.AppendLine(rawText);
            return sb.ToString();
        }

        /// <summary>
        /// Extrai o bloco JSON da resposta do LLM, ignorando texto antes/depois.
        /// Suporta respostas com ```json … ``` e respostas que começam/terminam em { }.
        /// </summary>
        private static string ExtractJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            // Tentar extrair bloco ```json ... ```
            var fenceStart = raw.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (fenceStart >= 0)
            {
                var contentStart = raw.IndexOf('\n', fenceStart) + 1;
                var fenceEnd = raw.IndexOf("```", contentStart, StringComparison.OrdinalIgnoreCase);
                if (fenceEnd > contentStart)
                    return raw.Substring(contentStart, fenceEnd - contentStart).Trim();
            }

            // Tentar extrair bloco ``` ... ```
            var fenceStart2 = raw.IndexOf("```", StringComparison.OrdinalIgnoreCase);
            if (fenceStart2 >= 0)
            {
                var contentStart2 = raw.IndexOf('\n', fenceStart2) + 1;
                var fenceEnd2 = raw.IndexOf("```", contentStart2, StringComparison.OrdinalIgnoreCase);
                if (fenceEnd2 > contentStart2)
                {
                    var candidate = raw.Substring(contentStart2, fenceEnd2 - contentStart2).Trim();
                    if (candidate.StartsWith("{"))
                        return candidate;
                }
            }

            // Tentar extrair o primeiro { ... } balanceado
            var start = raw.IndexOf('{');
            if (start >= 0)
            {
                var depth = 0;
                for (var i = start; i < raw.Length; i++)
                {
                    if (raw[i] == '{') depth++;
                    else if (raw[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                            return raw.Substring(start, i - start + 1);
                    }
                }
            }

            return raw.Trim();
        }

        private static DocumentStructure? TryDeserialize(string json, string? preferredLanguage)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dto = JsonSerializer.Deserialize<DocumentStructureDto>(json, options);
                if (dto == null) return null;

                var doc = new DocumentStructure
                {
                    Title = dto.Title,
                    Format = dto.Format,
                    Language = dto.Language ?? preferredLanguage
                };

                if (dto.Sections != null)
                {
                    foreach (var s in dto.Sections)
                    {
                        var section = new Section { Heading = s.Heading };

                        if (s.Paragraphs != null)
                            section.Paragraphs.AddRange(s.Paragraphs);

                        if (s.Lists != null)
                            foreach (var list in s.Lists)
                                if (list != null)
                                    section.Lists.Add(new System.Collections.Generic.List<string>(list));

                        if (s.CodeBlocks != null)
                            foreach (var cb in s.CodeBlocks)
                                section.CodeBlocks.Add(new CodeBlock
                                {
                                    Language = cb.Language,
                                    Code = cb.Code
                                });

                        doc.Sections.Add(section);
                    }
                }

                return doc.Sections.Count > 0 ? doc : null;
            }
            catch
            {
                return null;
            }
        }

        // DTO privados para desserialização do JSON do LLM
        private class DocumentStructureDto
        {
            public string? Title { get; set; }
            public string? Format { get; set; }
            public string? Language { get; set; }
            public SectionDto[]? Sections { get; set; }
        }

        private class SectionDto
        {
            public string? Heading { get; set; }
            public string[]? Paragraphs { get; set; }
            public string[][]? Lists { get; set; }
            public CodeBlockDto[]? CodeBlocks { get; set; }
        }

        private class CodeBlockDto
        {
            public string? Language { get; set; }
            public string? Code { get; set; }
        }
    }
}
