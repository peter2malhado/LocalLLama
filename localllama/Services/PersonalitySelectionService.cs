using System.Text.Json;
using localllama.Models;

namespace localllama.Services;

public static class PersonalitySelectionService
{
    private const string CustomPersonalitiesKey = "custom_ai_personalities";
    private const string SelectedPersonalityKey = "selected_ai_personality";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static readonly AiPersonalityOption DefaultPersonality = new()
    {
        Name = ChatPromptCatalog.DefaultPersonalityName,
        Description = "Equilibrado para perguntas gerais, explicações e apoio do dia a dia.",
        Prompt = ChatPromptCatalog.SystemPrompt
    };

    private static AiPersonalityOption _selected = LoadSelectedPersonality();

    public static AiPersonalityOption Selected
    {
        get => _selected;
        set
        {
            _selected = value ?? throw new ArgumentNullException(nameof(value));
            SaveSelectedPersonality(_selected);
        }
    }

    public static IReadOnlyList<AiPersonalityOption> GetAllPersonalities()
    {
        var builtIn = ChatPromptCatalog.GetBuiltInPersonalities();
        var custom = LoadCustomPersonalities();

        return builtIn
            .Concat(custom)
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public static void SaveCustomPersonality(AiPersonalityOption personality)
    {
        ArgumentNullException.ThrowIfNull(personality);

        var normalized = new AiPersonalityOption
        {
            Name = personality.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(personality.Description)
                ? "Personalidade criada pelo utilizador."
                : personality.Description.Trim(),
            Prompt = personality.Prompt.Trim(),
            IsCustom = true
        };

        var custom = LoadCustomPersonalities().ToList();
        var existingIndex = custom.FindIndex(p =>
            string.Equals(p.Name, normalized.Name, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
            custom[existingIndex] = normalized;
        else
            custom.Add(normalized);

        Preferences.Set(CustomPersonalitiesKey, JsonSerializer.Serialize(custom, JsonOptions));
    }

    public static void DeleteCustomPersonality(string personalityName)
    {
        if (string.IsNullOrWhiteSpace(personalityName))
            return;

        var custom = LoadCustomPersonalities()
            .Where(p => !string.Equals(p.Name, personalityName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        Preferences.Set(CustomPersonalitiesKey, JsonSerializer.Serialize(custom, JsonOptions));

        if (string.Equals(Selected.Name, personalityName.Trim(), StringComparison.OrdinalIgnoreCase))
            Selected = Clone(DefaultPersonality);
    }

    public static List<AiPersonalityOption> LoadCustomPersonalities()
    {
        try
        {
            var json = Preferences.Get(CustomPersonalitiesKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return new List<AiPersonalityOption>();

            var personalities = JsonSerializer.Deserialize<List<AiPersonalityOption>>(json, JsonOptions);
            return personalities?
                .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Prompt))
                .Select(p =>
                {
                    p.IsCustom = true;
                    return p;
                })
                .ToList() ?? new List<AiPersonalityOption>();
        }
        catch
        {
            return new List<AiPersonalityOption>();
        }
    }

    private static AiPersonalityOption LoadSelectedPersonality()
    {
        try
        {
            var json = Preferences.Get(SelectedPersonalityKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return Clone(DefaultPersonality);

            var selected = JsonSerializer.Deserialize<AiPersonalityOption>(json, JsonOptions);
            if (selected == null || string.IsNullOrWhiteSpace(selected.Name) || string.IsNullOrWhiteSpace(selected.Prompt))
                return Clone(DefaultPersonality);

            return selected;
        }
        catch
        {
            return Clone(DefaultPersonality);
        }
    }

    private static void SaveSelectedPersonality(AiPersonalityOption personality)
    {
        Preferences.Set(SelectedPersonalityKey, JsonSerializer.Serialize(personality, JsonOptions));
    }

    private static AiPersonalityOption Clone(AiPersonalityOption personality)
    {
        if (personality == null)
        {
            return new AiPersonalityOption
            {
                Name = ChatPromptCatalog.DefaultPersonalityName,
                Description = "Equilibrado para perguntas gerais, explicações e apoio do dia a dia.",
                Prompt = ChatPromptCatalog.SystemPrompt
            };
        }

        return new AiPersonalityOption
        {
            Name = personality.Name,
            Description = personality.Description,
            Prompt = personality.Prompt,
            IsCustom = personality.IsCustom
        };
    }
}
