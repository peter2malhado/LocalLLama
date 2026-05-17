using System.Collections.ObjectModel;
using localllama.Models;
using localllama.Services;

namespace localllama.ViewModels;

public class DocumentManagerViewModel : BindableObject
{
    private readonly RagDocumentService _ragDocumentService = new();

    public DocumentManagerViewModel()
    {
        LoadCommand = new Command(async () => await LoadAsync());
        ImportCommand = new Command(async () => await ImportAsync());
        DeleteCommand = new Command<RagDocumentEntry>(async entry => await DeleteAsync(entry));
    }

    public ObservableCollection<RagDocumentEntry> Documents { get; } = new();

    public Command LoadCommand { get; }
    public Command ImportCommand { get; }
    public Command<RagDocumentEntry> DeleteCommand { get; }

    public async Task LoadAsync()
    {
        Documents.Clear();
        var documents = await _ragDocumentService.LoadDocumentsAsync();
        foreach (var document in documents)
            Documents.Add(document);
    }

    private async Task ImportAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(RagDocumentPickerOptions.Create("Importar documento local"));

            if (result == null)
                return;

            await _ragDocumentService.ImportDocumentAsync(result);
            await LoadAsync();
            await ShowAlertAsync("Documento", "Documento importado com sucesso.");
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível importar o documento: {ex.Message}");
        }
    }

    private async Task DeleteAsync(RagDocumentEntry? entry)
    {
        if (entry == null)
            return;

        var confirm = await ShowConfirmAsync("Remover documento", $"Remover {entry.DisplayName}?");
        if (!confirm)
            return;

        try
        {
            await _ragDocumentService.DeleteDocumentAsync(entry.Id);
            Documents.Remove(entry);
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
}
