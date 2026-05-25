namespace localllama.Models;

public class AiPersonalityOption
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}
