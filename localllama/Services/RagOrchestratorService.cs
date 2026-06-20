using System.Text;

namespace localllama.Services;

public class RagOrchestratorService
{
    private readonly RagDocumentService _ragDocumentService;
    private readonly WebSearchService _webSearchService = new();

    public RagOrchestratorService(RagDocumentService ragDocumentService)
    {
        _ragDocumentService = ragDocumentService;
    }

    public async Task<string> BuildPromptAsync(string userInput)
        => await BuildPromptAsync(userInput, null);

    public async Task<string> BuildPromptAsync(string userInput, IReadOnlyCollection<long>? allowedDocumentIds)
    {
        var localContext = await _ragDocumentService.BuildContextBlockAsync(userInput, allowedDocumentIds: allowedDocumentIds);
        var webContext = await _webSearchService.SearchWebAsync(userInput);
        var memoryContext = await PersistentMemoryService.BuildContextBlockAsync(userInput);

        if (string.IsNullOrWhiteSpace(localContext) &&
            string.IsNullOrWhiteSpace(webContext) &&
            string.IsNullOrWhiteSpace(memoryContext))
            return userInput;

        var promptBuilder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(localContext))
        {
            promptBuilder.AppendLine("Contexto de documentos locais:");
            promptBuilder.AppendLine(localContext);
            promptBuilder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(webContext))
        {
            promptBuilder.AppendLine("Contexto de pesquisa na Web:");
            promptBuilder.AppendLine(webContext);
            promptBuilder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(memoryContext))
        {
            promptBuilder.AppendLine(memoryContext);
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Usa os contextos fornecidos acima apenas se forem relevantes para responder.");
        promptBuilder.AppendLine("Se a resposta não estiver no contexto, responde com base no teu conhecimento geral apenas quando fizer sentido.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Pergunta do utilizador:");
        promptBuilder.AppendLine(userInput);

        return promptBuilder.ToString();
    }
}
