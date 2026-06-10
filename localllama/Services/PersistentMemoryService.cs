using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace localllama.Services;

public static class PersistentMemoryService
{
    private const int MaxContextFacts = 8;
    private const int MaxStoredFacts = 64;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly IReadOnlyList<MemoryRule> Rules =
    [
        new(
            "project_name",
            "O projeto do utilizador chama-se {0}.",
            false,
            0.95,
            new Regex(
                @"\b(?:lembra-te(?:\s+que)?\s+|recorda(?:-te)?(?:\s+que)?\s+|não\s+te\s+esqueças(?:\s+que)?\s+)?(?:o\s+)?meu\s+projeto\s+(?:chama-se|chama\s+se|é)\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "name",
            "O nome do utilizador é {0}.",
            false,
            0.95,
            new Regex(
                @"\b(?:o\s+)?meu\s+nome\s+(?:é|e)\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "company_name",
            "A empresa do utilizador chama-se {0}.",
            false,
            0.9,
            new Regex(
                @"\b(?:a\s+)?minha\s+empresa\s+(?:chama-se|chama\s+se|é)\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "role",
            "O utilizador trabalha como {0}.",
            false,
            0.85,
            new Regex(
                @"\b(?:eu\s+)?trabalho\s+(?:como|com)\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "preference",
            "O utilizador prefere {0}.",
            true,
            0.72,
            new Regex(
                @"\b(?:eu\s+)?prefiro\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "preference",
            "O utilizador gosta de {0}.",
            true,
            0.66,
            new Regex(
                @"\b(?:eu\s+)?gosto\s+de\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "project_stack",
            "O projeto do utilizador usa {0}.",
            true,
            0.72,
            new Regex(
                @"\b(?:o\s+)?meu\s+projeto\s+(?:usa|utiliza|é\s+feito\s+com)\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "project_name_en",
            "The user's project is called {0}.",
            false,
            0.95,
            new Regex(
                @"\b(?:remember\s+that\s+)?(?:my\s+)?project\s+(?:is\s+called|is|named)\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "name_en",
            "The user's name is {0}.",
            false,
            0.95,
            new Regex(
                @"\b(?:my\s+)?name\s+is\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        new(
            "preference_en",
            "The user prefers {0}.",
            true,
            0.72,
            new Regex(
                @"\b(?:i\s+)?prefer\s+(?<value>[^.;!?\r\n]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled))
    ];

    public static async Task CaptureFromUserMessageAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || !UserContext.IsReady)
            return;

        var extractedFacts = ExtractFacts(userMessage);
        if (extractedFacts.Count == 0)
            return;

        await Gate.WaitAsync();
        try
        {
            var store = await LoadStoreUnsafeAsync();
            var changed = false;

            foreach (var fact in extractedFacts)
            {
                changed |= UpsertFact(store, fact);
            }

            if (changed)
                await SaveStoreUnsafeAsync(store);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<string> BuildContextBlockAsync(string userInput)
    {
        if (!UserContext.IsReady)
            return string.Empty;

        await Gate.WaitAsync();
        try
        {
            var store = await LoadStoreUnsafeAsync();
            if (store.Facts.Count == 0)
                return string.Empty;

            var relevantFacts = RankFacts(store.Facts, userInput)
                .Take(MaxContextFacts)
                .ToList();

            if (relevantFacts.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("Memória persistente do utilizador:");
            foreach (var fact in relevantFacts)
                builder.AppendLine($"- {fact.Summary}");

            builder.AppendLine("Usa estes factos como contexto persistente. Se entrarem em conflito com uma instrução nova do utilizador, segue a instrução nova.");
            return builder.ToString();
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<MemoryStore> LoadStoreUnsafeAsync()
    {
        var path = GetMemoryFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(path))
            return new MemoryStore();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var store = JsonSerializer.Deserialize<MemoryStore>(json, JsonOptions);
            return store ?? new MemoryStore();
        }
        catch
        {
            return new MemoryStore();
        }
    }

    private static async Task SaveStoreUnsafeAsync(MemoryStore store)
    {
        store.UpdatedAt = DateTimeOffset.UtcNow;

        var path = GetMemoryFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (store.Facts.Count > MaxStoredFacts)
        {
            store.Facts = store.Facts
                .OrderByDescending(f => f.LastSeenAt)
                .ThenByDescending(f => f.CreatedAt)
                .Take(MaxStoredFacts)
                .ToList();
        }

        var json = JsonSerializer.Serialize(store, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    private static List<MemoryFact> ExtractFacts(string userMessage)
    {
        var results = new List<MemoryFact>();

        foreach (var rule in Rules)
        {
            foreach (Match match in rule.Pattern.Matches(userMessage))
            {
                if (!match.Success)
                    continue;

                var value = NormalizeFactValue(match.Groups["value"].Value);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (LooksSensitive(value))
                    continue;

                results.Add(new MemoryFact
                {
                    Topic = rule.Topic,
                    Summary = string.Format(rule.SummaryTemplate, value),
                    Value = value,
                    SourceText = userMessage.Trim(),
                    Confidence = rule.Confidence,
                    AllowMultiple = rule.AllowMultiple
                });
            }
        }

        return results;
    }

    private static bool UpsertFact(MemoryStore store, MemoryFact incoming)
    {
        incoming.CreatedAt = DateTimeOffset.UtcNow;
        incoming.LastSeenAt = incoming.CreatedAt;
        incoming.UpdatedAt = incoming.CreatedAt;
        incoming.Id = Guid.NewGuid().ToString("N");

        var normalizedTopic = NormalizeKey(incoming.Topic);
        var normalizedValue = NormalizeKey(incoming.Value);

        if (!incoming.AllowMultiple)
        {
            var existing = store.Facts.FirstOrDefault(f => string.Equals(NormalizeKey(f.Topic), normalizedTopic, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Summary = incoming.Summary;
                existing.Value = incoming.Value;
                existing.SourceText = incoming.SourceText;
                existing.Confidence = Math.Max(existing.Confidence, incoming.Confidence);
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.LastSeenAt = DateTimeOffset.UtcNow;
                existing.Active = true;
                return true;
            }
        }

        var duplicate = store.Facts.FirstOrDefault(f =>
            string.Equals(NormalizeKey(f.Topic), normalizedTopic, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeKey(f.Value), normalizedValue, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            duplicate.Summary = incoming.Summary;
            duplicate.SourceText = incoming.SourceText;
            duplicate.Confidence = Math.Max(duplicate.Confidence, incoming.Confidence);
            duplicate.UpdatedAt = DateTimeOffset.UtcNow;
            duplicate.LastSeenAt = DateTimeOffset.UtcNow;
            duplicate.Active = true;
            return true;
        }

        store.Facts.Add(incoming);
        return true;
    }

    private static IEnumerable<MemoryFact> RankFacts(IEnumerable<MemoryFact> facts, string userInput)
    {
        var queryTokens = Tokenize(userInput).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return facts
            .Where(f => f.Active)
            .Select(f =>
            {
                var factTokens = Tokenize($"{f.Topic} {f.Summary} {f.Value}");
                var score = factTokens.Count(token => queryTokens.Contains(token));

                if (score == 0)
                {
                    var lowerQuery = userInput.ToLowerInvariant();
                    if (lowerQuery.Contains(f.Value.ToLowerInvariant()) || lowerQuery.Contains(f.Topic.ToLowerInvariant()))
                        score = 1;
                }

                return new RankedFact(f, score);
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Fact.LastSeenAt)
            .Select(item => item.Fact)
            .ToList();
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{Nd}]+")
            .Select(m => m.Value)
            .Where(token => token.Length > 2);
    }

    private static string NormalizeFactValue(string value)
    {
        var cleaned = value.Trim();
        cleaned = cleaned.Trim('"', '\'', '“', '”', '«', '»');
        cleaned = cleaned.TrimEnd('.', ';', ':', '!', '?', ',', ')', ']', '}');

        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ");

        return cleaned.Trim();
    }

    private static string NormalizeKey(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        while (normalized.Contains("  ", StringComparison.Ordinal))
            normalized = normalized.Replace("  ", " ");
        return normalized;
    }

    private static bool LooksSensitive(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower.Contains("password", StringComparison.Ordinal) ||
               lower.Contains("senha", StringComparison.Ordinal) ||
               lower.Contains("api key", StringComparison.Ordinal) ||
               lower.Contains("token", StringComparison.Ordinal) ||
               Regex.IsMatch(value, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(value, @"\b\d{6,}\b", RegexOptions.CultureInvariant);
    }

    private static string GetMemoryFilePath()
    {
        var userFolder = string.IsNullOrWhiteSpace(UserContext.Username)
            ? "default"
            : SanitizePathSegment(UserContext.Username);

        return Path.Combine(FileSystem.AppDataDirectory, "memory", userFolder, "memory.json");
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars);

        if (string.IsNullOrWhiteSpace(sanitized))
            return "default";

        return sanitized;
    }

    private sealed class MemoryRule(string topic, string summaryTemplate, bool allowMultiple, double confidence, Regex pattern)
    {
        public string Topic { get; } = topic;
        public string SummaryTemplate { get; } = summaryTemplate;
        public bool AllowMultiple { get; } = allowMultiple;
        public double Confidence { get; } = confidence;
        public Regex Pattern { get; } = pattern;
    }

    private sealed class RankedFact(MemoryFact fact, int score)
    {
        public MemoryFact Fact { get; } = fact;
        public int Score { get; } = score;
    }

    private sealed class MemoryStore
    {
        public int Version { get; set; } = 1;
        public List<MemoryFact> Facts { get; set; } = [];
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class MemoryFact
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Topic { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string SourceText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public bool AllowMultiple { get; set; }
        public bool Active { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
