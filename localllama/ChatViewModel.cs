using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using localllama;
using localllama.Models;
using localllama.Services;
using ChatSessionModel = localllama.Models.ChatSession;

public class ChatViewModel : BindableObject
{
    private readonly string _chatId;
    private readonly ChatConversationService _conversationService = new();
    private readonly RagDocumentService _ragDocumentService = new();
    private readonly DeveloperStatsService _developerStatsService = new();
    private readonly ChatInferenceService _inferenceService = new();
    private readonly RagOrchestratorService _ragOrchestratorService;
    private readonly WebSearchService _webSearchService = new();
    private ChatSessionModel? _currentChat;
    private string _currentMessage = string.Empty;
    private string? _selectedImagePath;
    private bool _llamaReady;
    private bool _isModelLoading;
    private string _loadingStatusText = "A carregar o modelo...";
    private string? _initErrorMessage;
    private bool _isDeveloperStatsVisible;
    private string _responseTimeText = "Aguardando";
    private string _tokenStatsText = "0";
    private string _contextStatsText = "0 / 0";
    private string _memoryUsageText = "0 MB";
    private string _personalityName = ChatPromptCatalog.DefaultPersonalityName;

    public ChatViewModel(string chatId)
    {
        _chatId = chatId;
        _ragOrchestratorService = new RagOrchestratorService(_ragDocumentService);

        SendMessageCommand = new Command(async () => await SendMessageAsync());
        ImportDocumentCommand = new Command(async () => await ImportDocumentAsync());
        ClearImageCommand = new Command(ClearSelectedImage);
    }

    public ObservableCollection<Message> Messages { get; } = new();

    public bool IsDeveloperStatsVisible
    {
        get => _isDeveloperStatsVisible;
        set
        {
            if (_isDeveloperStatsVisible == value)
                return;

            _isDeveloperStatsVisible = value;
            OnPropertyChanged();
        }
    }

    public string CurrentMessage
    {
        get => _currentMessage;
        set
        {
            if (_currentMessage == value)
                return;

            _currentMessage = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedImagePath
    {
        get => _selectedImagePath;
        set
        {
            if (_selectedImagePath == value)
                return;

            _selectedImagePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedImage));
        }
    }

    public bool HasSelectedImage => !string.IsNullOrWhiteSpace(_selectedImagePath);

    public string ResponseTimeText
    {
        get => _responseTimeText;
        set
        {
            if (_responseTimeText == value)
                return;

            _responseTimeText = value;
            OnPropertyChanged();
        }
    }

    public string TokenStatsText
    {
        get => _tokenStatsText;
        set
        {
            if (_tokenStatsText == value)
                return;

            _tokenStatsText = value;
            OnPropertyChanged();
        }
    }

    public string ContextStatsText
    {
        get => _contextStatsText;
        set
        {
            if (_contextStatsText == value)
                return;

            _contextStatsText = value;
            OnPropertyChanged();
        }
    }

    public string MemoryUsageText
    {
        get => _memoryUsageText;
        set
        {
            if (_memoryUsageText == value)
                return;

            _memoryUsageText = value;
            OnPropertyChanged();
        }
    }

    public bool IsModelLoading
    {
        get => _isModelLoading;
        private set
        {
            if (_isModelLoading == value)
                return;

            _isModelLoading = value;
            OnPropertyChanged();
        }
    }

    public string LoadingStatusText
    {
        get => _loadingStatusText;
        private set
        {
            if (_loadingStatusText == value)
                return;

            _loadingStatusText = value;
            OnPropertyChanged();
        }
    }

    public ICommand SendMessageCommand { get; }
    public ICommand ImportDocumentCommand { get; }
    public ICommand ClearImageCommand { get; }

    public string PersonalityName
    {
        get => _personalityName;
        set
        {
            if (_personalityName == value)
                return;

            _personalityName = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAndLoadAsync()
    {
        if (_currentChat != null || IsModelLoading || _llamaReady || _initErrorMessage != null)
        {
            if (_currentChat == null)
                await LoadSessionAsync();
            return;
        }

        IsModelLoading = true;
        LoadingStatusText = "A carregar o modelo...";

        try
        {
            await Task.Run(() => _inferenceService.Initialize());
            _llamaReady = true;
        }
        catch (Exception ex)
        {
            _llamaReady = false;
            _initErrorMessage = ChatInferenceService.BuildDetailedError(ex);
#if MACCATALYST || IOS
            if (!string.IsNullOrWhiteSpace(NativeLibraryHelper.LastDiagnostics))
                _initErrorMessage += $" | NativeDiag: {NativeLibraryHelper.LastDiagnostics}";
#endif
            Messages.Add(new Message
            {
                IsUser = false,
                Text = $"Erro ao carregar o modelo. {_initErrorMessage}"
            });
        }

        try
        {
            LoadingStatusText = "A preparar a conversa...";
            await LoadSessionAsync();
        }
        finally
        {
            IsModelLoading = false;
        }
    }

    private async Task LoadSessionAsync()
    {
        IsDeveloperStatsVisible = InferenceSettingsService.IsDeveloperStatsEnabled;
        _currentChat = await _conversationService.LoadOrCreateAsync(_chatId);
        PersonalityName = _currentChat.PersonalityName;
        _conversationService.PopulateMessages(Messages, _currentChat.Messages);
        RebuildInferenceSession();
        RefreshDeveloperStats();
    }

    private async Task SendMessageAsync()
    {
        if (!_llamaReady)
        {
            Messages.Add(new Message
            {
                IsUser = false,
                Text = IsModelLoading
                    ? "O modelo ainda está a carregar. Aguarda um momento."
                    : string.IsNullOrWhiteSpace(_initErrorMessage)
                        ? "Modelo não está carregado. Seleciona um modelo .gguf válido."
                        : $"Modelo não está carregado. {_initErrorMessage}"
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentMessage) && string.IsNullOrWhiteSpace(SelectedImagePath))
            return;

        if (_currentChat == null)
        {
            Messages.Add(new Message
            {
                IsUser = false,
                Text = "A conversa ainda está a ser preparada. Tenta novamente dentro de um instante."
            });
            return;
        }

        var userInput = CurrentMessage.Trim();
        var promptText = string.IsNullOrWhiteSpace(userInput) && !string.IsNullOrWhiteSpace(SelectedImagePath)
            ? "Descreve a imagem e responde de forma útil."
            : userInput;
        Messages.Add(new Message
        {
            Text = promptText,
            IsUser = true,
            ImagePath = SelectedImagePath
        });
        CurrentMessage = string.Empty;
        var attachedImagePath = SelectedImagePath;
        SelectedImagePath = null;

        await _conversationService.UpdateTitleIfNeededAsync(_currentChat, _chatId, promptText);
        await PersistentMemoryService.CaptureFromUserMessageAsync(promptText);

        var botMessage = new Message { Text = string.Empty, IsUser = false };
        Messages.Add(botMessage);

        try
        {
            if (InferenceSettingsService.IsWebSearchEnabled &&
                !string.IsNullOrWhiteSpace(InferenceSettingsService.WebSearchApiKey) &&
                _webSearchService.ShouldSearchWeb(userInput))
            {
                botMessage.Text = "A pesquisar na Web... 🌐";
            }

        var prompt = await _ragOrchestratorService.BuildPromptAsync(promptText);
        botMessage.Text = string.Empty;

        var result = attachedImagePath == null
            ? await _inferenceService.GenerateReplyAsync(prompt, partial => botMessage.Text = partial)
            : await _inferenceService.GenerateReplyWithImageAsync(prompt, attachedImagePath, partial => botMessage.Text = partial);

            botMessage.Text = result.FinalText;

            await _conversationService.SaveAsync(
                _chatId,
                _currentChat.Title,
                Messages,
                _currentChat.PersonalityName,
                _currentChat.PersonalityPrompt);
            _currentChat.Messages = Messages.Select(m => new ChatMessage
            {
                Role = m.IsUser ? "user" : "bot",
                Text = m.Text
            }).ToList();

            RebuildInferenceSession();
            ApplyDeveloperStats(_developerStatsService.Build(
                Messages,
                result.FinalText,
                _inferenceService.EffectiveSettings?.ContextSize ?? 0,
                result.Elapsed));
        }
        catch (Exception ex)
        {
            botMessage.Text = $"Erro ao gerar resposta: {ex.Message}";
        }
    }

    private async Task ImportDocumentAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(RagDocumentPickerOptions.Create("Adicionar documento ou foto"));
            if (result == null)
                return;

            if (IsImageFile(result.FileName))
            {
                await AttachImageAsync(result);
                return;
            }

            var entry = await _ragDocumentService.ImportDocumentAsync(result);
            await ShowAlertAsync("Documento", $"{entry.DisplayName} foi adicionado ao RAG local.");
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Erro", $"Não foi possível adicionar o documento: {ex.Message}");
        }
    }

    private void RebuildInferenceSession()
    {
        if (_currentChat == null)
            return;

        _inferenceService.RebuildSession(_currentChat.Messages, _currentChat.PersonalityPrompt);
    }

    private static bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AttachImageAsync(FileResult result)
    {
        var storageDir = Path.Combine(FileSystem.AppDataDirectory, "chat-images", UserContext.Username ?? "default", _chatId);
        Directory.CreateDirectory(storageDir);

        var ext = Path.GetExtension(result.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".jpg";

        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}{ext}";
        var destinationPath = Path.Combine(storageDir, fileName);

        await using var source = await result.OpenReadAsync();
        await using var destination = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination);

        SelectedImagePath = destinationPath;
    }

    private void ClearSelectedImage()
    {
        SelectedImagePath = null;
    }

    private void RefreshDeveloperStats()
    {
        var contextSize = _inferenceService.EffectiveSettings?.ContextSize ?? 0;
        ApplyDeveloperStats(_developerStatsService.Build(Messages, string.Empty, contextSize, TimeSpan.Zero));
    }

    private void ApplyDeveloperStats(ChatDeveloperStats stats)
    {
        ResponseTimeText = stats.ResponseTimeText;
        TokenStatsText = stats.TokenStatsText;
        ContextStatsText = stats.ContextStatsText;
        MemoryUsageText = stats.MemoryUsageText;
    }

    private static Task ShowAlertAsync(string title, string message)
    {
        var page = Application.Current?.MainPage;
        if (page == null)
            return Task.CompletedTask;

        return page.DisplayAlert(title, message, "OK");
    }
}
