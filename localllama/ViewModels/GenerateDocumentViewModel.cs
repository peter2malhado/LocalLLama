using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using localllama.Models;
using localllama.Services;

namespace localllama.ViewModels;

public class GenerateDocumentViewModel : INotifyPropertyChanged
{
    private readonly ChatInferenceService _chatInferenceService = new();
    private readonly DocumentGenerationService _service;
    private readonly GeneratedDocumentService _generatedDocumentService = new();
    private string _inputText = string.Empty;
    private string _selectedFormat = "Word (.docx)";
    private string _selectedCodeExtension = ".cs";
    private bool _isBusy;
    private string _statusText = string.Empty;
    private bool _isModelLoading;
    private string _loadingStatusText = "A carregar o modelo...";
    private bool _modelReady;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Formats { get; } = new() { "Word (.docx)", "PDF (.pdf)", "Código" };
    public ObservableCollection<string> CodeExtensions { get; } = new() { ".cs", ".py", ".rs" };
    public ObservableCollection<GeneratedFileInfo> GeneratedDocuments { get; } = new();

    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText == value)
                return;

            _inputText = value;
            OnPropertyChanged(nameof(InputText));
        }
    }

    public string SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (_selectedFormat == value)
                return;

            _selectedFormat = value;
            OnPropertyChanged(nameof(SelectedFormat));
            OnPropertyChanged(nameof(IsCodeFormat));
        }
    }

    public string SelectedCodeExtension
    {
        get => _selectedCodeExtension;
        set
        {
            if (_selectedCodeExtension == value)
                return;

            _selectedCodeExtension = value;
            OnPropertyChanged(nameof(SelectedCodeExtension));
        }
    }

    public bool IsCodeFormat => string.Equals(SelectedFormat, "Código", StringComparison.OrdinalIgnoreCase);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsNotBusy));
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool IsNotBusy => !_isBusy;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value)
                return;

            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(_statusText) && !IsBusy;

    public bool IsModelLoading
    {
        get => _isModelLoading;
        set
        {
            if (_isModelLoading == value)
                return;

            _isModelLoading = value;
            OnPropertyChanged(nameof(IsModelLoading));
        }
    }

    public string LoadingStatusText
    {
        get => _loadingStatusText;
        set
        {
            if (_loadingStatusText == value)
                return;

            _loadingStatusText = value;
            OnPropertyChanged(nameof(LoadingStatusText));
        }
    }

    public ICommand GenerateCommand { get; }
    public ICommand LoadGeneratedDocumentsCommand { get; }
    public ICommand ExportDocumentCommand { get; }
    public ICommand OpenDocumentCommand { get; }
    public ICommand DeleteDocumentCommand { get; }

    public GenerateDocumentViewModel()
    {
        _service = new DocumentGenerationService(_chatInferenceService);
        GenerateCommand = new Command(async () => await ExecuteGenerateAsync());
        LoadGeneratedDocumentsCommand = new Command(async () => await LoadGeneratedDocumentsAsync());
        ExportDocumentCommand = new Command<GeneratedFileInfo>(async file => await ExportAsync(file));
        OpenDocumentCommand = new Command<GeneratedFileInfo>(async file => await OpenAsync(file));
        DeleteDocumentCommand = new Command<GeneratedFileInfo>(async file => await DeleteAsync(file));
    }

    public async Task InitializeAsync()
    {
        if (IsModelLoading)
            return;

        if (_modelReady)
        {
            await LoadGeneratedDocumentsAsync();
            return;
        }

        IsModelLoading = true;
        LoadingStatusText = "A carregar o modelo...";

        try
        {
            await Task.Run(() => _chatInferenceService.Initialize());
            _modelReady = true;
            LoadingStatusText = "Modelo pronto.";
        }
        catch (Exception ex)
        {
            LoadingStatusText = $"Modelo indisponível: {ex.Message}";
            await ShowAlertAsync("Erro", $"Falha ao carregar o modelo: {ex.Message}");
        }
        finally
        {
            IsModelLoading = false;
            await LoadGeneratedDocumentsAsync();
        }
    }

    private async Task ExecuteGenerateAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(InputText))
        {
            await ShowAlertAsync("Erro", "Conteúdo vazio.");
            return;
        }

        if (!Formats.Contains(SelectedFormat))
        {
            await ShowAlertAsync("Erro", "Formato não suportado.");
            return;
        }

        if (IsCodeFormat && !CodeExtensions.Contains(SelectedCodeExtension))
        {
            await ShowAlertAsync("Erro", "Extensão de código não suportada.");
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "A preparar geração...";

            var request = new DocumentGenerationRequest
            {
                RawText = InputText,
                OutputFormat = SelectedFormat,
                CodeExtension = IsCodeFormat ? SelectedCodeExtension : null
            };

            await _service.GenerateAsync(request, status =>
            {
                MainThread.BeginInvokeOnMainThread(() => StatusText = status);
            });

            await LoadGeneratedDocumentsAsync();
            StatusText = "Ficheiro gerado.";
        }
        catch (Exception ex)
        {
            StatusText = string.Empty;
            await ShowAlertAsync("Erro", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadGeneratedDocumentsAsync()
    {
        GeneratedDocuments.Clear();
        var documents = await _generatedDocumentService.LoadAsync();
        foreach (var document in documents)
            GeneratedDocuments.Add(document);
    }

    private async Task ExportAsync(GeneratedFileInfo? file)
    {
        if (file == null)
            return;

        try
        {
            await _generatedDocumentService.ExportAsync(file);
            await ShowAlertAsync("Exportado", $"Exportação pronta: {Path.GetFileName(file.Path)}");
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível exportar: {ex.Message}");
        }
    }

    private async Task OpenAsync(GeneratedFileInfo? file)
    {
        if (file == null)
            return;

        try
        {
            await _generatedDocumentService.OpenAsync(file);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível abrir: {ex.Message}");
        }
    }

    private async Task DeleteAsync(GeneratedFileInfo? file)
    {
        if (file == null)
            return;

        var confirm = await ShowConfirmAsync("Apagar documento", $"Apagar {Path.GetFileName(file.Path)}?");
        if (!confirm)
            return;

        try
        {
            await _generatedDocumentService.DeleteAsync(file);
            await LoadGeneratedDocumentsAsync();
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível apagar: {ex.Message}");
        }
    }

    private static Task ShowAlertAsync(string title, string message)
    {
        var page = Application.Current?.MainPage;
        return page == null ? Task.CompletedTask : page.DisplayAlert(title, message, "OK");
    }

    private static Task<bool> ShowConfirmAsync(string title, string message)
    {
        var page = Application.Current?.MainPage;
        return page == null ? Task.FromResult(false) : page.DisplayAlert(title, message, "Apagar", "Cancelar");
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
