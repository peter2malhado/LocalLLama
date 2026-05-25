using localllama.Models;

namespace localllama.Services;

public static class PersonalitySelectionService
{
    private static AiPersonalityOption _selected = new()
    {
        Name = ChatPromptCatalog.DefaultPersonalityName,
        Description = "Equilibrado para perguntas gerais, explicações e apoio do dia a dia.",
        Prompt = ChatPromptCatalog.SystemPrompt
    };

    public static AiPersonalityOption Selected
    {
        get => _selected;
        set => _selected = value ?? throw new ArgumentNullException(nameof(value));
    }
}
