namespace localllama.Models;

public sealed record DocumentGenerationResult(
    string FilePath,
    string FileName,
    string MimeType);
