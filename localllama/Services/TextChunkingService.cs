namespace localllama.Services;

public static class TextChunkingService
{
    public static List<string> SplitIntoChunks(string text, int chunkSize = 700, int overlap = 120)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        var normalized = text.Replace("\r\n", "\n").Trim();
        var start = 0;

        while (start < normalized.Length)
        {
            var length = Math.Min(chunkSize, normalized.Length - start);
            var candidateEnd = start + length;

            if (candidateEnd < normalized.Length)
            {
                var breakIndex = normalized.LastIndexOfAny(new[] { '\n', '.', '!', '?', ';', ' ' }, candidateEnd - 1, length);
                if (breakIndex > start + (chunkSize / 2))
                    candidateEnd = breakIndex + 1;
            }

            var chunk = normalized[start..candidateEnd].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            if (candidateEnd >= normalized.Length)
                break;

            start = Math.Max(candidateEnd - overlap, start + 1);
        }

        return chunks;
    }
}
