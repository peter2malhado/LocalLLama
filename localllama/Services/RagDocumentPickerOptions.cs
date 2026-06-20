namespace localllama.Services;

public static class RagDocumentPickerOptions
{
    public static PickOptions Create(string title)
    {
        return new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.MacCatalyst, new[] { "public.plain-text", "net.daringfireball.markdown", "public.json", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document", "public.image" } },
                { DevicePlatform.iOS, new[] { "public.plain-text", "net.daringfireball.markdown", "public.json", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document", "public.image" } },
                { DevicePlatform.Android, new[] { "text/plain", "application/json", "text/markdown", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "image/*" } },
                { DevicePlatform.WinUI, new[] { ".txt", ".md", ".json", ".pdf", ".docx", ".png", ".jpg", ".jpeg", ".webp", ".bmp" } }
            })
        };
    }
}
