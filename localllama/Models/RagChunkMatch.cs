namespace localllama.Models;

public class RagChunkMatch
{
    public long DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Score { get; set; }
}
