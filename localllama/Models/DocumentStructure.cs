using System.Collections.Generic;

namespace localllama.Models
{
    public class DocumentStructure
    {
        public string? Title { get; set; }
        public string? Format { get; set; }
        public string? Language { get; set; }
        public List<Section> Sections { get; set; } = new List<Section>();
    }

    public class Section
    {
        public string? Heading { get; set; }
        public List<string> Paragraphs { get; set; } = new List<string>();
        public List<List<string>> Lists { get; set; } = new List<List<string>>();
        public List<CodeBlock> CodeBlocks { get; set; } = new List<CodeBlock>();
    }

    public class CodeBlock
    {
        public string? Language { get; set; }
        public string? Code { get; set; }
    }
}