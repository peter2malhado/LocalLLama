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
                { DevicePlatform.MacCatalyst, new[] { "public.plain-text", "net.daringfireball.markdown", "public.json", "com.adobe.pdf" } },
                { DevicePlatform.iOS, new[] { "public.plain-text", "net.daringfireball.markdown", "public.json", "com.adobe.pdf" } },
                { DevicePlatform.Android, new[] { "text/plain", "application/json", "text/markdown", "application/pdf" } },
                { DevicePlatform.WinUI, new[] { ".txt", ".md", ".json", ".pdf" } }
            })
        };
    }
}
