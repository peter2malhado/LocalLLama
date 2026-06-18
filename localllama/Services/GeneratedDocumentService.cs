using CommunityToolkit.Maui.Storage;
using localllama.Models;

namespace localllama.Services;

public class GeneratedDocumentService
{
    private static string CurrentUserId => UserContext.Username ?? "default";

    public string GetStorageDirectory()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "generated-documents", CurrentUserId);
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task<GeneratedFileInfo> SaveAsync(string sourcePath, string fileName, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Caminho inválido.");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Ficheiro gerado não encontrado.", sourcePath);

        var storageDir = GetStorageDirectory();
        var destinationPath = BuildUniquePath(storageDir, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);

        await Task.CompletedTask;

        return new GeneratedFileInfo
        {
            DisplayName = Path.GetFileNameWithoutExtension(destinationPath),
            Path = destinationPath,
            Mime = mimeType,
            SizeText = FormatSize(new FileInfo(destinationPath).Length),
            DateText = File.GetLastWriteTime(destinationPath).ToString("yyyy-MM-dd HH:mm")
        };
    }

    public async Task<List<GeneratedFileInfo>> LoadAsync()
    {
        return await Task.Run(() =>
        {
            var dir = GetStorageDirectory();
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir)
                : Array.Empty<string>();

            return files
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Select(path => new GeneratedFileInfo
                {
                    DisplayName = Path.GetFileNameWithoutExtension(path),
                    Path = path,
                    Mime = GetMimeType(path),
                    SizeText = FormatSize(new FileInfo(path).Length),
                    DateText = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm")
                })
                .ToList();
        });
    }

    public Task ExportAsync(GeneratedFileInfo fileInfo)
    {
        if (fileInfo == null)
            throw new ArgumentNullException(nameof(fileInfo));

        return ExportAsync(fileInfo.Path, Path.GetFileName(fileInfo.Path), fileInfo.Mime);
    }

    public Task OpenAsync(GeneratedFileInfo fileInfo)
    {
        if (fileInfo == null)
            throw new ArgumentNullException(nameof(fileInfo));

        return OpenAsync(fileInfo.Path);
    }

    public async Task OpenAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Caminho inválido.");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Ficheiro não encontrado.", sourcePath);

        await Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(sourcePath)
        });
    }

    public Task DeleteAsync(GeneratedFileInfo fileInfo)
    {
        if (fileInfo == null)
            throw new ArgumentNullException(nameof(fileInfo));

        return DeleteAsync(fileInfo.Path);
    }

    public Task DeleteAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Caminho inválido.");

        if (File.Exists(sourcePath))
            File.Delete(sourcePath);

        return Task.CompletedTask;
    }

    public async Task ExportAsync(string sourcePath, string fileName, string mimeType)
    {
        await using var stream = File.OpenRead(sourcePath);
        var saveResult = await FileSaver.Default.SaveAsync(fileName, stream);
        if (saveResult.IsSuccessful)
            return;

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = fileName,
            File = new ShareFile(sourcePath, mimeType)
        });
    }

    private static string BuildUniquePath(string directory, string fileName)
    {
        var safeFileName = SanitizeFileName(fileName);
        var path = Path.Combine(directory, safeFileName);
        if (!File.Exists(path))
            return path;

        var name = Path.GetFileNameWithoutExtension(safeFileName);
        var ext = Path.GetExtension(safeFileName);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directory, $"{name} {Guid.NewGuid():N}{ext}");
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');

        return fileName.Trim();
    }

    private static string GetMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pdf" => "application/pdf",
            ".cs" => "text/plain",
            ".py" => "text/plain",
            ".rs" => "text/plain",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0.0} KB";
        return $"{bytes / 1024d / 1024d:0.0} MB";
    }
}
