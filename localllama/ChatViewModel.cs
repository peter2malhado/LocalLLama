using System.Collections.ObjectModel;
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

        if (string.IsNullOrWhiteSpace(CurrentMessage))
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
        Messages.Add(new Message { Text = userInput, IsUser = true });
        CurrentMessage = string.Empty;

        await _conversationService.UpdateTitleIfNeededAsync(_currentChat, _chatId, userInput);
        await PersistentMemoryService.CaptureFromUserMessageAsync(userInput);

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

            var prompt = await _ragOrchestratorService.BuildPromptAsync(userInput);
            botMessage.Text = string.Empty;

            var result = await _inferenceService.GenerateReplyAsync(prompt, partial => botMessage.Text = partial);

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
            var result = await FilePicker.Default.PickAsync(RagDocumentPickerOptions.Create("Adicionar documento ao RAG local"));
            if (result == null)
                return;

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
