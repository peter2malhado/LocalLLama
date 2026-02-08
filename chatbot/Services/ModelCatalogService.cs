using System.Net.Http.Json;
using System.Text.Json;
using chatbot.Models;

namespace chatbot.Services
{
    public class ModelCatalogService
    {
        private readonly HttpClient _httpClient;

        // Substitui pelo URL real do teu JSON no GitHub
        private const string CatalogUrl = "https://raw.githubusercontent.com/peter2malhado/o-json-para-o-chatbot/refs/heads/main/modelos.json";

        public ModelCatalogService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<AIModel>> FetchModelsAsync(CancellationToken ct = default)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = await _httpClient.GetAsync(CatalogUrl, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var models = await JsonSerializer.DeserializeAsync<List<AIModel>>(stream, options, ct);

            return models ?? new List<AIModel>();
        }
    }
}
