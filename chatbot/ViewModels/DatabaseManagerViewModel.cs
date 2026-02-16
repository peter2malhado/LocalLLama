using System.Collections.ObjectModel;
using chatbot.Models;
using chatbot.Services;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Storage;

namespace chatbot.ViewModels
{
    public class DatabaseManagerViewModel : BindableObject
    {
        public ObservableCollection<DatabaseEntry> Databases { get; } = new();

        public Command LoadCommand { get; }
        public Command CreateCommand { get; }
        public Command<DatabaseEntry> UseCommand { get; }
        public Command<DatabaseEntry> RenameCommand { get; }
        public Command<DatabaseEntry> DeleteCommand { get; }
        public Command ExportCommand { get; }
        public Command ImportCommand { get; }

        public DatabaseManagerViewModel()
        {
            LoadCommand = new Command(async () => await LoadAsync());
            CreateCommand = new Command(async () => await CreateAsync());
            UseCommand = new Command<DatabaseEntry>(async e => await UseAsync(e));
            RenameCommand = new Command<DatabaseEntry>(async e => await RenameAsync(e));
            DeleteCommand = new Command<DatabaseEntry>(async e => await DeleteAsync(e));
            ExportCommand = new Command(async () => await ExportAsync());
            ImportCommand = new Command(async () => await ImportAsync());
        }

        public async Task LoadAsync()
        {
            Databases.Clear();
            DatabaseConfig.EnsureDatabaseLayout();

            var dir = DatabaseConfig.DatabasesDirectory;
            var files = Directory.GetFiles(dir, "*.db", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var isSelected = string.Equals(
                    Path.GetFileName(file),
                    DatabaseConfig.SelectedDatabaseName,
                    StringComparison.OrdinalIgnoreCase);

                Databases.Add(new DatabaseEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FileName = Path.GetFileName(file),
                    SizeText = FormatSize(info.Length),
                    DateText = info.LastWriteTime.ToString("yyyy-MM-dd"),
                    IsSelected = isSelected
                });
            }

            await Task.CompletedTask;
        }

        private async Task CreateAsync()
        {
            var name = await PromptAsync("Nova base de dados", "Nome da base (sem extensão):");
            if (string.IsNullOrWhiteSpace(name))
                return;

            var fileName = $"{name}.db";
            var path = Path.Combine(DatabaseConfig.DatabasesDirectory, fileName);

            if (File.Exists(path))
            {
                await ShowAlertAsync("Erro", "Já existe uma base com esse nome.");
                return;
            }

            File.WriteAllBytes(path, Array.Empty<byte>());

            DatabaseConfig.SelectedDatabaseName = fileName;
            DatabaseHelper.InitializeDatabase();
            await LoadAsync();
        }

        private async Task UseAsync(DatabaseEntry? entry)
        {
            if (entry == null)
                return;

            DatabaseConfig.SelectedDatabaseName = entry.FileName;
            DatabaseHelper.InitializeDatabase();
            await LoadAsync();
        }

        private async Task RenameAsync(DatabaseEntry? entry)
        {
            if (entry == null)
                return;

            var newName = await PromptAsync("Renomear base", "Novo nome (sem extensão):", entry.Name);
            if (string.IsNullOrWhiteSpace(newName))
                return;

            var dir = DatabaseConfig.DatabasesDirectory;
            var oldPath = Path.Combine(dir, entry.FileName);
            var newFileName = $"{newName}.db";
            var newPath = Path.Combine(dir, newFileName);

            if (File.Exists(newPath))
            {
                await ShowAlertAsync("Erro", "Já existe uma base com esse nome.");
                return;
            }

            File.Move(oldPath, newPath);

            if (entry.IsSelected)
            {
                DatabaseConfig.SelectedDatabaseName = newFileName;
            }

            await LoadAsync();
        }

        private async Task DeleteAsync(DatabaseEntry? entry)
        {
            if (entry == null)
                return;

            var confirm = await ShowConfirmAsync("Remover", $"Remover {entry.FileName}?");
            if (!confirm)
                return;

            var path = Path.Combine(DatabaseConfig.DatabasesDirectory, entry.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (entry.IsSelected)
            {
                DatabaseConfig.SelectedDatabaseName = "chats.db";
                DatabaseHelper.InitializeDatabase();
            }

            await LoadAsync();
        }

        private async Task ExportAsync()
        {
            try
            {
                var dbPath = DatabaseHelper.GetDatabasePath();
                if (!File.Exists(dbPath))
                {
                    await ShowAlertAsync("Erro", "Base de dados não encontrada.");
                    return;
                }

                var fileName = $"{Path.GetFileNameWithoutExtension(dbPath)}_export_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                await using var stream = File.OpenRead(dbPath);

                var result = await FileSaver.Default.SaveAsync(fileName, stream);
                if (result.IsSuccessful)
                {
                    await ShowAlertAsync("Exportado", $"Ficheiro guardado em:\n{result.FilePath}");
                }
                else if (result.Exception != null)
                {
                    await ShowAlertAsync("Erro", $"Falha ao exportar: {result.Exception.Message}");
                }
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Erro", $"Falha ao exportar: {ex.Message}");
            }
        }

        private async Task ImportAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Importar base de dados",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.MacCatalyst, new[] { "db" } },
                        { DevicePlatform.iOS, new[] { "db" } },
                        { DevicePlatform.Android, new[] { "application/octet-stream" } },
                        { DevicePlatform.WinUI, new[] { ".db" } },
                    })
                });

                if (result == null)
                    return;

                var name = Path.GetFileName(result.FileName);
                if (!name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                {
                    await ShowAlertAsync("Erro", "Ficheiro inválido.");
                    return;
                }

                DatabaseConfig.EnsureDatabaseLayout();
                var destPath = Path.Combine(DatabaseConfig.DatabasesDirectory, name);

                // Evitar lock
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                await Task.Delay(150);

                var tempPath = destPath + ".importing";
                await using (var src = await result.OpenReadAsync())
                await using (var dest = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await src.CopyToAsync(dest);
                }

                if (File.Exists(destPath))
                    File.Delete(destPath);
                File.Move(tempPath, destPath);

                DatabaseConfig.SelectedDatabaseName = name;
                DatabaseHelper.InitializeDatabase();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Erro", $"Falha ao importar: {ex.Message}");
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

        private static Task<string?> PromptAsync(string title, string message, string? initial = null)
        {
            var page = Application.Current?.MainPage;
            if (page == null)
                return Task.FromResult<string?>(null);

            return page.DisplayPromptAsync(title, message, "OK", "Cancelar", initial, keyboard: Keyboard.Default);
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
