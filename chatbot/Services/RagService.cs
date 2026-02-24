using System.Text;
using chatbot.Services;
using LLama;
using LLama.Common;
using Microsoft.Data.Sqlite;
using UglyToad.PdfPig;
using Xceed.Words.NET;

namespace chatbot.Services;

public static class RagService
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static LLamaWeights? _weights;
    private static LLamaEmbedder? _embedder;
    private static bool _initialized;

    public static async Task<int> ImportDocumentAsync(string filePath)
    {
        await EnsureInitializedAsync();
        DatabaseHelper.InitializeRagDatabase();

        var text = await Task.Run(() => ExtractText(filePath));
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var chunks = ChunkText(text, 500, 80);
        if (chunks.Count == 0)
            return 0;

        var docId = Guid.NewGuid().ToString("N");
        var docName = Path.GetFileName(filePath);

        using var connection = DatabaseHelper.GetRagConnection();
        using var tx = connection.BeginTransaction();

        var insertDoc = new SqliteCommand(
            "INSERT INTO RagDocuments (Id, Name, Path) VALUES (@Id, @Name, @Path)",
            connection, tx);
        insertDoc.Parameters.AddWithValue("@Id", docId);
        insertDoc.Parameters.AddWithValue("@Name", docName);
        insertDoc.Parameters.AddWithValue("@Path", filePath);
        insertDoc.ExecuteNonQuery();

        var insertChunk = new SqliteCommand(
            "INSERT INTO RagChunks (Id, DocId, ChunkIndex, TextEncrypted, Embedding) VALUES (@Id, @DocId, @ChunkIndex, @TextEncrypted, @Embedding)",
            connection, tx);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            var embeddingList = await Task.Run(() => _embedder!.GetEmbeddings(chunkText));
            var embedding = ReduceEmbedding(embeddingList);
            var encrypted = ChatCrypto.EncryptText(chunkText);

            insertChunk.Parameters.Clear();
            insertChunk.Parameters.AddWithValue("@Id", $"{docId}_{i}");
            insertChunk.Parameters.AddWithValue("@DocId", docId);
            insertChunk.Parameters.AddWithValue("@ChunkIndex", i);
            insertChunk.Parameters.AddWithValue("@TextEncrypted", encrypted);
            insertChunk.Parameters.AddWithValue("@Embedding", ToBytes(embedding));
            insertChunk.ExecuteNonQuery();
        }

        tx.Commit();
        return chunks.Count;
    }

    public static async Task<string?> GetContextAsync(string query, int topK = 3, double minScore = 0.2)
    {
        await EnsureInitializedAsync();

        if (!await HasDocumentsAsync())
            return null;

        var queryEmbeddingList = await Task.Run(() => _embedder!.GetEmbeddings(query));
        var queryEmbedding = ReduceEmbedding(queryEmbeddingList);

        using var connection = DatabaseHelper.GetRagConnection();
        var cmd = new SqliteCommand(
            "SELECT TextEncrypted, Embedding FROM RagChunks",
            connection);

        var candidates = new List<(double score, string text)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var textEncrypted = reader.GetString(0);
            var embeddingBlob = (byte[])reader[1];

            var embedding = FromBytes(embeddingBlob);
            var score = CosineSimilarity(queryEmbedding, embedding);

            if (score >= minScore)
                candidates.Add((score, ChatCrypto.DecryptText(textEncrypted)));
        }

        var best = candidates
            .OrderByDescending(c => c.score)
            .Take(topK)
            .Select(c => c.text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (best.Count == 0)
            return null;

        var sb = new StringBuilder();
        foreach (var b in best)
        {
            sb.AppendLine(b);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    public static async Task<bool> HasDocumentsAsync()
    {
        DatabaseHelper.InitializeRagDatabase();
        using var connection = DatabaseHelper.GetRagConnection();
        var cmd = new SqliteCommand("SELECT COUNT(*) FROM RagChunks", connection);
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        return count > 0;
    }

    private static async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await InitLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var modelPath = ModelConfig.SelectedModelPath;
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                throw new FileNotFoundException("Modelo .gguf não encontrado. Seleciona um modelo primeiro.");

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 512,
                GpuLayerCount = 10
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _embedder = new LLamaEmbedder(_weights, parameters);
            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static string ExtractText(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => ExtractPdfText(filePath),
            ".docx" => ExtractDocxText(filePath),
            _ => string.Empty
        };
    }

    private static string ExtractPdfText(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(filePath);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static string ExtractDocxText(string filePath)
    {
        using var doc = DocX.Load(filePath);
        return doc.Text ?? string.Empty;
    }

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var clean = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var chunks = new List<string>();

        var index = 0;
        while (index < clean.Length)
        {
            var len = Math.Min(chunkSize, clean.Length - index);
            var chunk = clean.Substring(index, len).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            index += Math.Max(1, chunkSize - overlap);
        }

        return chunks;
    }

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] FromBytes(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static float[] ReduceEmbedding(IReadOnlyList<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            return Array.Empty<float>();

        if (embeddings.Count == 1)
            return embeddings[0];

        var length = embeddings[0].Length;
        var sum = new float[length];

        foreach (var emb in embeddings)
        {
            if (emb.Length != length) continue;
            for (var i = 0; i < length; i++)
                sum[i] += emb[i];
        }

        var count = embeddings.Count;
        for (var i = 0; i < length; i++)
            sum[i] /= count;

        return sum;
    }
}
