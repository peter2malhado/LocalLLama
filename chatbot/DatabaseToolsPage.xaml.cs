using chatbot.Services;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Storage;

namespace chatbot
{
    public partial class DatabaseToolsPage : ContentPage
    {
        public DatabaseToolsPage()
        {
            InitializeComponent();
            DbPathLabel.Text = $"Local da BD: {DatabaseHelper.GetDatabasePath()}";
        }

        private async void OnExportClicked(object sender, EventArgs e)
        {
            try
            {
                var dbPath = DatabaseHelper.GetDatabasePath();
                if (!File.Exists(dbPath))
                {
                    await DisplayAlert("Erro", "Base de dados não encontrada.", "OK");
                    return;
                }

                var fileName = $"chats_export_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                await using var stream = File.OpenRead(dbPath);

                var result = await FileSaver.Default.SaveAsync(fileName, stream);
                if (result.IsSuccessful)
                {
                    await DisplayAlert("Exportado", $"Ficheiro guardado em:\n{result.FilePath}", "OK");
                }
                else if (result.Exception != null)
                {
                    await DisplayAlert("Erro", $"Falha ao exportar: {result.Exception.Message}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Falha ao exportar: {ex.Message}", "OK");
            }
        }

        private async void OnImportClicked(object sender, EventArgs e)
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

                var dbPath = DatabaseHelper.GetDatabasePath();

                // Garante que não há conexões ativas ao SQLite
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                await Task.Delay(150);

                var tempPath = dbPath + ".importing";
                await using (var src = await result.OpenReadAsync())
                await using (var dest = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await src.CopyToAsync(dest);
                }

                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
                File.Move(tempPath, dbPath);

                DatabaseHelper.InitializeDatabase();

                await DisplayAlert("Importado", "Base de dados carregada com sucesso. Volta atrás e atualiza a lista.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Falha ao importar: {ex.Message}", "OK");
            }
        }
    }
}
