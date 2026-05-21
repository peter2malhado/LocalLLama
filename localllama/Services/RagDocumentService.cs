using localllama.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;
using UglyToad.PdfPig;

namespace localllama.Services;

public class RagDocumentService
{
    private static string CurrentUserId => UserContext.Username ?? "default";

    public async Task<RagDocumentEntry> ImportDocumentAsync(FileResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        var extension = Path.GetExtension(result.FileName);
        if (!IsSupportedExtension(extension))
            throw new InvalidOperationException("Formato não suportado. Usa .txt, .md ou .json.");

        var documentsDir = GetDocumentsDirectory();
        Directory.CreateDirectory(documentsDir);

        var uniqueName = BuildUniqueFileName(documentsDir, result.FileName);
        var destinationPath = Path.Combine(documentsDir, uniqueName);

        await using (var src = await result.OpenReadAsync())
        await using (var dest = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await src.CopyToAsync(dest);
        }

        string content;
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new StringBuilder();
            using (var pdf = PdfDocument.Open(destinationPath))
            {
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
            }
            content = sb.ToString();
        }
        else
        {
            await using (var stream = File.OpenRead(destinationPath))
            using (var reader = new StreamReader(stream))
            {
                content = await reader.ReadToEndAsync();
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException("O documento está vazio.");
        }

        var chunks = TextChunkingService.SplitIntoChunks(content);
        if (chunks.Count == 0)
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException("Não foi possível extrair conteúdo útil do documento.");
        }

        return await Task.Run(() =>
        {
            using var connection = DatabaseHelper.GetUserConnection();
            using var transaction = connection.BeginTransaction();

            var insertDocument = new SqliteCommand(
                """
                INSERT INTO RagDocuments (UserId, Name, FileName, FilePath, FileExtension, FileSizeBytes)
                VALUES (@UserId, @Name, @FileName, @FilePath, @FileExtension, @FileSizeBytes);
                SELECT last_insert_rowid();
                """,
                connection,
                transaction);
            insertDocument.Parameters.AddWithValue("@UserId", CurrentUserId);
            insertDocument.Parameters.AddWithValue("@Name", Path.GetFileNameWithoutExtension(uniqueName));
            insertDocument.Parameters.AddWithValue("@FileName", uniqueName);
            insertDocument.Parameters.AddWithValue("@FilePath", destinationPath);
            insertDocument.Parameters.AddWithValue("@FileExtension", extension);
            insertDocument.Parameters.AddWithValue("@FileSizeBytes", new FileInfo(destinationPath).Length);

            var documentId = (long)(insertDocument.ExecuteScalar() ?? 0L);

            for (var i = 0; i < chunks.Count; i++)
            {
                var insertChunk = new SqliteCommand(
                    """
                    INSERT INTO RagChunks (DocumentId, UserId, ChunkIndex, Text, TokenEstimate)
                    VALUES (@DocumentId, @UserId, @ChunkIndex, @Text, @TokenEstimate);
                    """,
                    connection,
                    transaction);
                insertChunk.Parameters.AddWithValue("@DocumentId", documentId);
                insertChunk.Parameters.AddWithValue("@UserId", CurrentUserId);
                insertChunk.Parameters.AddWithValue("@ChunkIndex", i);
                insertChunk.Parameters.AddWithValue("@Text", chunks[i]);
                insertChunk.Parameters.AddWithValue("@TokenEstimate", EstimateTokenCount(chunks[i]));
                insertChunk.ExecuteNonQuery();
            }

            transaction.Commit();

            return new RagDocumentEntry
            {
                Id = documentId,
                Name = Path.GetFileNameWithoutExtension(uniqueName),
                FileName = uniqueName,
                Extension = extension,
                SizeText = FormatSize(new FileInfo(destinationPath).Length),
                DateText = File.GetLastWriteTime(destinationPath).ToString("yyyy-MM-dd"),
                ChunkCount = chunks.Count
            };
        });
    }

    public async Task<List<RagDocumentEntry>> LoadDocumentsAsync()
    {
        return await Task.Run(() =>
        {
            var documents = new List<RagDocumentEntry>();
            using var connection = DatabaseHelper.GetUserConnection();

            var command = new SqliteCommand(
                """
                SELECT d.Id, d.Name, d.FileName, d.FileExtension, d.FileSizeBytes, d.CreatedAt,
                       COUNT(c.Id) as ChunkCount
                FROM RagDocuments d
                LEFT JOIN RagChunks c ON c.DocumentId = d.Id
                WHERE d.UserId = @UserId
                GROUP BY d.Id, d.Name, d.FileName, d.FileExtension, d.FileSizeBytes, d.CreatedAt
                ORDER BY d.CreatedAt DESC, d.Id DESC;
                """,
                connection);
            command.Parameters.AddWithValue("@UserId", CurrentUserId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var createdAt = reader.IsDBNull(5) ? DateTime.MinValue : ParseSqliteDateTime(reader.GetValue(5));
                documents.Add(new RagDocumentEntry
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    FileName = reader.GetString(2),
                    Extension = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    SizeText = FormatSize(reader.IsDBNull(4) ? 0 : reader.GetInt64(4)),
                    DateText = createdAt == DateTime.MinValue ? string.Empty : createdAt.ToString("yyyy-MM-dd"),
                    ChunkCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                });
            }

            return documents;
        });
    }

    public async Task DeleteDocumentAsync(long documentId)
    {
        await Task.Run(() =>
        {
            using var connection = DatabaseHelper.GetUserConnection();

            string? filePath = null;
            var fileQuery = new SqliteCommand(
                "SELECT FilePath FROM RagDocuments WHERE Id = @Id AND UserId = @UserId",
                connection);
            fileQuery.Parameters.AddWithValue("@Id", documentId);
            fileQuery.Parameters.AddWithValue("@UserId", CurrentUserId);
            filePath = fileQuery.ExecuteScalar() as string;

            using var transaction = connection.BeginTransaction();

            var deleteChunks = new SqliteCommand(
                "DELETE FROM RagChunks WHERE DocumentId = @DocumentId AND UserId = @UserId",
                connection,
                transaction);
            deleteChunks.Parameters.AddWithValue("@DocumentId", documentId);
            deleteChunks.Parameters.AddWithValue("@UserId", CurrentUserId);
            deleteChunks.ExecuteNonQuery();

            var deleteDocument = new SqliteCommand(
                "DELETE FROM RagDocuments WHERE Id = @Id AND UserId = @UserId",
                connection,
                transaction);
            deleteDocument.Parameters.AddWithValue("@Id", documentId);
            deleteDocument.Parameters.AddWithValue("@UserId", CurrentUserId);
            deleteDocument.ExecuteNonQuery();

            transaction.Commit();

            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                File.Delete(filePath);
        });
    }

    public async Task<List<RagChunkMatch>> SearchRelevantChunksAsync(string query, int maxResults = 4)
    {
        return await Task.Run(() =>
        {
            var normalizedTerms = NormalizeTerms(query);
            if (normalizedTerms.Count == 0)
                return new List<RagChunkMatch>();

            using var connection = DatabaseHelper.GetUserConnection();
            var command = new SqliteCommand(
                """
                SELECT c.DocumentId, d.Name, d.FileName, c.ChunkIndex, c.Text
                FROM RagChunks c
                INNER JOIN RagDocuments d ON d.Id = c.DocumentId
                WHERE c.UserId = @UserId
                ORDER BY d.CreatedAt DESC, c.ChunkIndex ASC;
                """,
                connection);
            command.Parameters.AddWithValue("@UserId", CurrentUserId);

            var matches = new List<RagChunkMatch>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var text = reader.GetString(4);
                var score = ScoreChunk(text, normalizedTerms, query);
                if (score <= 0)
                    continue;

                matches.Add(new RagChunkMatch
                {
                    DocumentId = reader.GetInt64(0),
                    DocumentName = reader.GetString(1),
                    FileName = reader.GetString(2),
                    ChunkIndex = reader.GetInt32(3),
                    Text = text,
                    Score = score
                });
            }

            return matches
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.DocumentName)
                .ThenBy(m => m.ChunkIndex)
                .Take(maxResults)
                .ToList();
        });
    }

    public async Task<string?> BuildContextBlockAsync(string query, int maxResults = 4)
    {
        var matches = await SearchRelevantChunksAsync(query, maxResults);
        if (matches.Count == 0)
            return null;

        var parts = new List<string>();
        foreach (var match in matches)
        {
            parts.Add($"[Documento: {match.FileName} | Bloco {match.ChunkIndex + 1}]\n{match.Text}");
        }

        return string.Join("\n\n", parts);
    }

    private static string GetDocumentsDirectory()
    {
        var userName = UserContext.Username ?? "default";
        return Path.Combine(FileSystem.AppDataDirectory, "documents", userName);
    }

    private static string BuildUniqueFileName(string directory, string originalFileName)
    {
        var name = Path.GetFileNameWithoutExtension(originalFileName);
        var extension = Path.GetExtension(originalFileName);
        var candidate = $"{name}{extension}";
        var counter = 1;

        while (File.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{name}_{counter}{extension}";
            counter++;
        }

        return candidate;
    }

    private static bool IsSupportedExtension(string? extension)
    {
        return extension != null && SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] SupportedExtensions = [".txt", ".md", ".json", ".pdf"];

    private static int ScoreChunk(string chunkText, IReadOnlyCollection<string> normalizedTerms, string originalQuery)
    {
        var normalizedChunk = NormalizeText(chunkText);
        var score = 0;

        foreach (var term in normalizedTerms)
        {
            if (normalizedChunk.Contains(term, StringComparison.Ordinal))
                score += term.Length > 4 ? 3 : 2;
        }

        var phrase = NormalizeText(originalQuery);
        if (!string.IsNullOrWhiteSpace(phrase) && normalizedChunk.Contains(phrase, StringComparison.Ordinal))
            score += 5;

        return score;
    }

    private static List<string> NormalizeTerms(string text)
    {
        return NormalizeText(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length >= 2)
            .Distinct()
            .ToList();
    }

    private static string NormalizeText(string text)
    {
        var formD = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(ch switch
            {
                '\r' or '\n' or ',' or '.' or ':' or ';' or '!' or '?' or '(' or ')' or '"' or '\'' or '[' or ']' or '{' or '}' or '/' or '\\' => ' ',
                _ => ch
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4d));
    }

    private static string FormatSize(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        if (bytes >= gb) return $"{bytes / (double)gb:0.00} GB";
        if (bytes >= mb) return $"{bytes / (double)mb:0.00} MB";
        if (bytes >= kb) return $"{bytes / (double)kb:0.00} KB";
        return $"{bytes} B";
    }

    private static DateTime ParseSqliteDateTime(object value)
    {
        return value switch
        {
            DateTime dateTime => dateTime,
            string text when DateTime.TryParse(text, out var parsed) => parsed,
            _ => DateTime.MinValue
        };
    }
}
