namespace localllama.Models
{
    public class DocumentGenerationRequest
    {
        public string RawText { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = string.Empty; // "Word (.docx)", "PDF (.pdf)", "Código"
        public string? CodeExtension { get; set; } // .cs, .py, .rs
    }
}