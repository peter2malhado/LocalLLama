using System.Collections.ObjectModel;
using chatbot.Models;
using chatbot.Services;

namespace chatbot.ViewModels;

public class ModelManagerViewModel
{
    private readonly ModelCatalogService _catalogService;
    private readonly ModelDownloadService _downloadService;

    public ModelManagerViewModel()
    {
        var httpClient = new HttpClient();
        _catalogService = new ModelCatalogService(httpClient);
        _downloadService = new ModelDownloadService(httpClient);

        LoadModelsCommand = new Command(async () => await LoadModelsAsync());
        DownloadCommand = new Command<AIModel>(async m => await DownloadAsync(m));
    }

    public ObservableCollection<AIModel> Models { get; } = new();

    public Command LoadModelsCommand { get; }
    public Command<AIModel> DownloadCommand { get; }

    public async Task LoadModelsAsync()
    {
        try
        {
            var list = await _catalogService.FetchModelsAsync();

            Models.Clear();
            foreach (var item in list)
            {
                item.Progress = 0;
                item.IsDownloading = false;
                Models.Add(item);
            }
        }
        catch (HttpRequestException)
        {
            await ShowAlertAsync("Erro", "Sem ligação à internet.");
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Falha ao carregar catálogo: {ex.Message}");
        }
    }

    private async Task DownloadAsync(AIModel? model)
    {
        if (model == null || model.IsDownloading)
            return;

        model.IsDownloading = true;

        try
        {
            var progress = new Progress<double>(p => model.Progress = p);
            await _downloadService.DownloadAsync(model, progress);
            await ShowAlertAsync("Sucesso", $"Download concluído: {model.FileName}");
        }
        catch (HttpRequestException)
        {
            await ShowAlertAsync("Erro", "Sem ligação à internet.");
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Falha no download: {ex.Message}");
        }
        finally
        {
            model.IsDownloading = false;
        }
    }

    private static Task ShowAlertAsync(string title, string message)
    {
        var page = Application.Current?.MainPage;
        if (page == null)
            return Task.CompletedTask;

        return page.DisplayAlert(title, message, "OK");
    }
}