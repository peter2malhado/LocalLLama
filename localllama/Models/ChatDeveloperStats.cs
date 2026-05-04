namespace localllama.Models;

public class ChatDeveloperStats
{
    public string ResponseTimeText { get; init; } = "Aguardando";
    public string TokenStatsText { get; init; } = "0";
    public string ContextStatsText { get; init; } = "0 / 0";
    public string MemoryUsageText { get; init; } = "0 MB";
}
