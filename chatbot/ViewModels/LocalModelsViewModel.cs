using System.Collections.ObjectModel;
using chatbot.Models;
using chatbot.Services;
using Microsoft.Maui.Storage;

namespace chatbot.ViewModels
{
    public class LocalModelsViewModel : BindableObject
    {
        public ObservableCollection<AIModel> LocalModels { get; } = new();

        public Command LoadLocalModelsCommand { get; }
        public Command ImportModelCommand { get; }
        public Command<AIModel> UseModelCommand { get; }
        public Command<AIModel> RemoveModelCommand { get; }

        public LocalModelsViewModel()
        {
            LoadLocalModelsCommand = new Command(async () => await LoadLocalModelsAsync());
            ImportModelCommand = new Command(async () => await ImportModelAsync());
            UseModelCommand = new Command<AIModel>(async m => await UseModelAsync(m));
            RemoveModelCommand = new Command<AIModel>(async m => await RemoveModelAsync(m));
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

            var files = Directory.GetFiles(modelsDir, "*.gguf", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var isSelected = string.Equals(file, ModelConfig.SelectedModelPath, StringComparison.OrdinalIgnoreCase);
                LocalModels.Add(new AIModel
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FileName = Path.GetFileName(file),
                    Description = "Modelo local",
                    Url = string.Empty,
                    SizeText = FormatSize(info.Length),
                    DateText = info.LastWriteTime.ToString("yyyy-MM-dd"),
                    IsSelected = isSelected
                });
            }
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
                        { DevicePlatform.WinUI, new[] { ".gguf" } },
                    })
                });

                if (result == null)
                    return;

                if (!result.FileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                {
                    await ShowAlertAsync("Formato inválido", "Escolhe um ficheiro .gguf.");
                    return;
                }

                var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
                Directory.CreateDirectory(modelsDir);

                var destPath = Path.Combine(modelsDir, result.FileName);

                await using var src = await result.OpenReadAsync();
                await using var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await src.CopyToAsync(dest);

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
                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }

                if (string.Equals(ModelConfig.SelectedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    ModelConfig.SelectedModelPath = null;
                }

                await LoadLocalModelsAsync();
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Erro", $"Não foi possível remover: {ex.Message}");
            }
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
}
