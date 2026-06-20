using System.Collections.ObjectModel;
using System.Text.Json;
using localllama.Models;
using localllama.Services;

namespace localllama.ViewModels;

public class LocalModelsViewModel : BindableObject
{
    private bool _isImporting;
    private double _importProgress;
    private string _importStatusText = "A importar modelo...";

    public LocalModelsViewModel()
    {
        LoadLocalModelsCommand = new Command(async () => await LoadLocalModelsAsync());
        ImportModelCommand = new Command(async () => await ImportModelAsync());
        UseModelCommand = new Command<AIModel>(async m => await UseModelAsync(m));
        RemoveModelCommand = new Command<AIModel>(async m => await RemoveModelAsync(m));
        ImportMmprojCommand = new Command<AIModel>(async m => await ImportMmprojAsync(m));
    }

    public ObservableCollection<AIModel> LocalModels { get; } = new();

    public Command LoadLocalModelsCommand { get; }
    public Command ImportModelCommand { get; }
    public Command<AIModel> UseModelCommand { get; }
    public Command<AIModel> RemoveModelCommand { get; }
    public Command<AIModel> ImportMmprojCommand { get; }

    public bool IsImporting
    {
        get => _isImporting;
        set
        {
            if (_isImporting == value) return;
            _isImporting = value;
            OnPropertyChanged();
        }
    }

    public double ImportProgress
    {
        get => _importProgress;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (Math.Abs(_importProgress - clamped) < 0.0001) return;
            _importProgress = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImportProgressText));
        }
    }

    public string ImportProgressText => $"{ImportProgress:P0}";

    public string ImportStatusText
    {
        get => _importStatusText;
        set
        {
            if (_importStatusText == value) return;
            _importStatusText = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadLocalModelsAsync()
    {
        LocalModels.Clear();

        var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        if (!Directory.Exists(modelsDir))
        {
            Directory.CreateDirectory(modelsDir);
            return;
        }

        var files = Directory.GetFiles(modelsDir, "*", SearchOption.TopDirectoryOnly)
            .Where(IsModelFile);

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            var isSelected = string.Equals(file, ModelConfig.SelectedModelPath, StringComparison.OrdinalIgnoreCase);
            var mmprojPath = LoadAssociatedMmprojPath(file);
            LocalModels.Add(new AIModel
            {
                Name = Path.GetFileNameWithoutExtension(file),
                FileName = Path.GetFileName(file),
                Description = "Modelo local",
                Url = string.Empty,
                SizeText = FormatSize(info.Length),
                DateText = info.LastWriteTime.ToString("yyyy-MM-dd"),
                HasMmproj = !string.IsNullOrWhiteSpace(mmprojPath),
                MmprojFileName = mmprojPath == null ? string.Empty : Path.GetFileName(mmprojPath),
                IsSelected = isSelected
            });
        }
    }

    private static bool IsModelFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
            && !name.Contains(".mmproj", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ImportModelAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Importar modelo .gguf",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.MacCatalyst, new[] { "gguf" } },
                    { DevicePlatform.iOS, new[] { "gguf" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream" } },
                    { DevicePlatform.WinUI, new[] { ".gguf" } }
                })
            });

            if (result == null)
                return;

            if (!result.FileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            {
                await ShowAlertAsync("Formato inválido", "Escolhe um ficheiro .gguf.");
                return;
            }

            IsImporting = true;
            ImportStatusText = $"A importar {result.FileName}";
            ImportProgress = 0;

            await Task.Yield();

            var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
            Directory.CreateDirectory(modelsDir);

            var destPath = Path.Combine(modelsDir, result.FileName);

            await using var src = await result.OpenReadAsync();
            await using var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[1024 * 1024];
            var totalBytes = src.CanSeek ? src.Length : 0L;
            var totalRead = 0L;
            int read;

            while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                if (totalBytes > 0)
                    ImportProgress = (double)totalRead / totalBytes;
            }

            await dest.FlushAsync();
            ImportProgress = 1;

            var sourceSize = totalBytes > 0 ? totalBytes : totalRead;
            var destSize = new FileInfo(destPath).Length;
            if (destSize != sourceSize)
            {
                File.Delete(destPath);
                throw new IOException($"Cópia incompleta. Fonte {sourceSize} bytes, destino {destSize} bytes.");
            }

            ModelConfig.SelectedModelPath = destPath;
            await ShowAlertAsync("Modelo importado", result.FileName);

            await LoadLocalModelsAsync();
        }
        catch (HttpRequestException)
        {
            await ShowAlertAsync("Erro", "Sem ligação à internet.");
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível importar: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
            ImportStatusText = "A importar modelo...";
            ImportProgress = 0;
        }
    }

    private async Task UseModelAsync(AIModel? model)
    {
        if (model == null)
            return;

        var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        var modelPath = Path.Combine(modelsDir, model.FileName);

        if (!File.Exists(modelPath))
        {
            await ShowAlertAsync("Erro", "Ficheiro não encontrado.");
            return;
        }

        ModelConfig.SelectedModelPath = modelPath;
        await LoadLocalModelsAsync();
    }

    private async Task RemoveModelAsync(AIModel? model)
    {
        if (model == null)
            return;

        var confirm = await ShowConfirmAsync("Remover", $"Remover {model.FileName}?");
        if (!confirm)
            return;

        var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        var modelPath = Path.Combine(modelsDir, model.FileName);

        try
        {
            if (File.Exists(modelPath)) File.Delete(modelPath);

            if (string.Equals(ModelConfig.SelectedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                ModelConfig.SelectedModelPath = null;

            await LoadLocalModelsAsync();
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível remover: {ex.Message}");
        }
    }

    private async Task ImportMmprojAsync(AIModel? model)
    {
        if (model == null)
            return;

        var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        var modelPath = Path.Combine(modelsDir, model.FileName);
        if (!File.Exists(modelPath))
        {
            await ShowAlertAsync("Erro", "Modelo não encontrado.");
            return;
        }

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = $"Importar mmproj para {model.FileName}",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.MacCatalyst, new[] { "mmproj", "bin", "gguf" } },
                { DevicePlatform.iOS, new[] { "mmproj", "bin", "gguf" } },
                { DevicePlatform.Android, new[] { "application/octet-stream" } },
                { DevicePlatform.WinUI, new[] { ".mmproj", ".bin", ".gguf" } }
            })
        });

        if (result == null)
            return;

        var targetPath = GetAssociatedMmprojPath(modelPath);
        try
        {
            await using var src = await result.OpenReadAsync();
            await using var dest = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await src.CopyToAsync(dest);
            await dest.FlushAsync();

            SaveAssociatedMmprojPath(modelPath, targetPath);
            await LoadLocalModelsAsync();
            await ShowAlertAsync("mmproj associado", Path.GetFileName(targetPath));
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível importar o mmproj: {ex.Message}");
        }
    }

    private static string GetAssociatedMmprojPath(string modelPath)
    {
        var dir = Path.GetDirectoryName(modelPath) ?? FileSystem.AppDataDirectory;
        var baseName = Path.GetFileNameWithoutExtension(modelPath);
        return Path.Combine(dir, $"{baseName}.mmproj.gguf");
    }

    private static string GetMmprojMetadataPath(string modelPath)
        => $"{modelPath}.mmproj.json";

    private static string? LoadAssociatedMmprojPath(string modelPath)
    {
        var metadataPath = GetMmprojMetadataPath(modelPath);
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            var data = JsonSerializer.Deserialize<MmprojAssociation>(json);
            if (data == null || string.IsNullOrWhiteSpace(data.MmprojPath))
                return null;

            return File.Exists(data.MmprojPath) ? data.MmprojPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveAssociatedMmprojPath(string modelPath, string mmprojPath)
    {
        var metadataPath = GetMmprojMetadataPath(modelPath);
        var data = new MmprojAssociation { MmprojPath = mmprojPath };
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(data));
    }

    private sealed class MmprojAssociation
    {
        public string MmprojPath { get; set; } = string.Empty;
    }

    private static Task ShowAlertAsync(string title, string message)
    {
        var page = Application.Current?.MainPage;
        if (page == null)
            return Task.CompletedTask;

        return page.DisplayAlert(title, message, "OK");
    }

    private static Task<bool> ShowConfirmAsync(string title, string message)
    {
        var page = Application.Current?.MainPage;
        if (page == null)
            return Task.FromResult(false);

        return page.DisplayAlert(title, message, "Remover", "Cancelar");
    }

    private static string FormatSize(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (bytes >= GB) return $"{bytes / (double)GB:0.00} GB";
        if (bytes >= MB) return $"{bytes / (double)MB:0.00} MB";
        if (bytes >= KB) return $"{bytes / (double)KB:0.00} KB";
        return $"{bytes} B";
    }
}
