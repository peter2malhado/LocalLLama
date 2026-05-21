using System.Text;
using System.Text.Json;

namespace localllama.Services;

public class WebSearchService
{
    private readonly HttpClient _httpClient = new();

    public async Task<string?> SearchWebAsync(string query, int maxResults = 3)
    {
        var isEnabled = InferenceSettingsService.IsWebSearchEnabled;
        var apiKey = InferenceSettingsService.WebSearchApiKey;

        if (!isEnabled || string.IsNullOrWhiteSpace(apiKey))
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
