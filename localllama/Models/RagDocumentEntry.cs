namespace localllama.Models;

public class RagDocumentEntry
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(Extension) ? Name : $"{Name}{Extension}";
}
