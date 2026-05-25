using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace localllama.Services;

public class WebSearchService
{
    private readonly HttpClient _httpClient = new();

    private static readonly string[] SearchIntentKeywords =
    {
        "hoje", "agora", "atual", "atualizado", "ultimas", "últimas", "ultimos", "últimos",
        "noticias", "notícias", "preço", "precos", "preços", "cotação", "cotacao", "tempo",
        "meteorologia", "resultado", "resultados", "placar", "jogo", "jogos", "classificação",
        "classificacao", "ranking", "tendência", "tendencia", "lançamento", "lancamento",
        "release", "versão", "versao", "documentação", "documentacao", "api", "site", "web",
        "internet", "pesquisa", "pesquisar", "procura", "procurar", "encontra", "encontrar",
        "compara", "comparar", "restaurante", "hotel", "voo", "voos", "agenda", "horário",
        "horario", "mercado", "bolsa", "bitcoin", "ações", "acoes", "euro", "dólar", "dolar"
    };

    private static readonly string[] GreetingPhrases =
    {
        "ola", "olá", "boas", "bom dia", "boa tarde", "boa noite", "tudo bem", "como estas",
        "como estás", "hey", "oi", "yo"
    };

    public async Task<string?> SearchWebAsync(string query, int maxResults = 3)
    {
        var isEnabled = InferenceSettingsService.IsWebSearchEnabled;
        var apiKey = InferenceSettingsService.WebSearchApiKey;

        if (!isEnabled || string.IsNullOrWhiteSpace(apiKey) || !ShouldSearchWeb(query))
            return null;

        try
        {
            var requestBody = new
            {
                api_key = apiKey,
                query = query,
                search_depth = "basic",
                max_results = maxResults
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            
            // Set up a timeout to avoid hanging the UI or conversation
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await _httpClient.PostAsync("https://api.tavily.com/search", content);

            if (!response.IsSuccessStatusCode)
                return null;

            var jsonString = await response.Content.ReadAsStringAsync();
            
            // Gravar o log raw da resposta JSON em segundo plano
            _ = Task.Run(() => LogRawResponse(query, jsonString));

            using var doc = JsonDocument.Parse(jsonString);
            
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return null;

            var contextBuilder = new StringBuilder();
            var index = 1;
            foreach (var result in results.EnumerateArray())
            {
                string? title = null;
                string? url = null;
                string? rawContent = null;

                if (result.TryGetProperty("title", out var titleProp))
                    title = titleProp.GetString();
                if (result.TryGetProperty("url", out var urlProp))
                    url = urlProp.GetString();
                if (result.TryGetProperty("content", out var contentProp))
                    rawContent = contentProp.GetString();

                if (!string.IsNullOrWhiteSpace(rawContent))
                {
                    contextBuilder.AppendLine($"[Web Fonte #{index}]: {title ?? "Sem Título"}");
                    if (!string.IsNullOrWhiteSpace(url))
                        contextBuilder.AppendLine($"URL: {url}");
                    contextBuilder.AppendLine($"Conteúdo: {rawContent}");
                    contextBuilder.AppendLine();
                    index++;
                }
            }

            return contextBuilder.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao pesquisar na web: {ex.Message}");
            return null;
        }
    }

    public bool ShouldSearchWeb(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var normalized = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (GreetingPhrases.Any(phrase => normalized.Equals(phrase, StringComparison.Ordinal) ||
                                          normalized.StartsWith($"{phrase} ", StringComparison.Ordinal)))
        {
            return false;
        }

        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount <= 3 && !ContainsSearchIntent(normalized))
            return false;

        if (!normalized.Contains('?') && wordCount <= 5 && !ContainsSearchIntent(normalized))
            return false;

        return ContainsSearchIntent(normalized) || LooksLikeCurrentInfoQuestion(normalized);
    }

    private static bool ContainsSearchIntent(string normalizedQuery)
    {
        return SearchIntentKeywords.Any(keyword => normalizedQuery.Contains(keyword, StringComparison.Ordinal));
    }

    private static bool LooksLikeCurrentInfoQuestion(string normalizedQuery)
    {
        return normalizedQuery.Contains("quem e", StringComparison.Ordinal) ||
               normalizedQuery.Contains("quem é", StringComparison.Ordinal) ||
               normalizedQuery.Contains("qual e o", StringComparison.Ordinal) ||
               normalizedQuery.Contains("qual é o", StringComparison.Ordinal) ||
               normalizedQuery.Contains("quando", StringComparison.Ordinal) ||
               normalizedQuery.Contains("onde", StringComparison.Ordinal) ||
               Regex.IsMatch(normalizedQuery, @"\b(202[4-9]|203\d)\b", RegexOptions.CultureInvariant);
    }

    private static string Normalize(string query)
    {
        return query.Trim().ToLowerInvariant();
    }

    private static void LogRawResponse(string query, string jsonResponse)
    {
        try
        {
            var logsDir = Path.Combine(FileSystem.AppDataDirectory, "web_search_logs");
            Directory.CreateDirectory(logsDir);

            // Sanitizar a query para o nome de ficheiro
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedQuery = new string(query
                .Select(c => invalidChars.Contains(c) ? '_' : c)
                .ToArray())
                .Replace(" ", "_");

            if (sanitizedQuery.Length > 30)
                sanitizedQuery = sanitizedQuery[..30];

            var fileName = $"search_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{sanitizedQuery}.json";
            var filePath = Path.Combine(logsDir, fileName);

            File.WriteAllText(filePath, jsonResponse);
            System.Diagnostics.Debug.WriteLine($"Log da pesquisa web gravado em: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao gravar log da pesquisa: {ex.Message}");
        }
    }
}
