using System.Diagnostics;
using localllama.Models;

namespace localllama.Services;

public class DeveloperStatsService
{
    public ChatDeveloperStats Build(IEnumerable<Message> messages, string botReply, uint contextSize, TimeSpan elapsed)
    {
        var responseTokens = EstimateTokenCount(botReply);
        var historyTokens = EstimateTokenCount(ChatPromptCatalog.SystemPrompt);
        historyTokens += messages.Sum(m => EstimateTokenCount(m.Text));

        return new ChatDeveloperStats
        {
            ResponseTimeText = elapsed == TimeSpan.Zero ? "Aguardando" : $"{elapsed.TotalSeconds:0.00}s",
            TokenStatsText = responseTokens > 0 ? responseTokens.ToString() : "0",
            ContextStatsText = $"{historyTokens} / {contextSize}",
            MemoryUsageText = FormatBytes(Process.GetCurrentProcess().WorkingSet64)
        };
    }

    private static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var normalized = text.Trim();
        var byChars = (int)Math.Ceiling(normalized.Length / 4d);
        var byWords = normalized.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, Math.Max(byChars, byWords));
    }

    private static string FormatBytes(long bytes)
    {
        const double mb = 1024d * 1024d;
        const double gb = mb * 1024d;

        if (bytes >= gb)
            return $"{bytes / gb:0.00} GB";

        return $"{bytes / mb:0.0} MB";
    }
}
