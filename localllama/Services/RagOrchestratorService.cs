namespace localllama.Services;

public class RagOrchestratorService
{
    private readonly RagDocumentService _ragDocumentService;

    public RagOrchestratorService(RagDocumentService ragDocumentService)
    {
        _ragDocumentService = ragDocumentService;
    }

    public async Task<string> BuildPromptAsync(string userInput)
    {
        var ragContext = await _ragDocumentService.BuildContextBlockAsync(userInput);
        if (string.IsNullOrWhiteSpace(ragContext))
            return userInput;

        return
            $"""
             Contexto de documentos locais:
             {ragContext}

             Usa este contexto apenas se for relevante para responder.
             Se a resposta não estiver nos documentos, diz isso claramente e responde com base no teu conhecimento geral apenas quando fizer sentido.

             Pergunta do utilizador:
             {userInput}
             """;
    }
}
