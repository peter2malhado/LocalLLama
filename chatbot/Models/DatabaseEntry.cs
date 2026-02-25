namespace chatbot.Models;

public class DatabaseEntry
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}