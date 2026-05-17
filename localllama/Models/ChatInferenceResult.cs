namespace localllama.Models;

public class ChatInferenceResult
{
    public string FinalText { get; init; } = string.Empty;
    public TimeSpan Elapsed { get; init; }
}
