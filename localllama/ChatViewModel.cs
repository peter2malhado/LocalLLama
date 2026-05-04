using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using localllama;
using localllama.Models;
using localllama.Services;
using LLama;
using LLama.Common;
using ChatSession = localllama.Models.ChatSession;


public class ChatViewModel : BindableObject
{
    private const string DefaultChatTitle = "Nova Conversa";
    private const string SystemPrompt =
        """
        És Bob, um assistente de IA útil, calmo e inteligente.
        Responde em português de Portugal por defeito, a menos que o utilizador peça outra língua.
        Mantém um tom natural, amigável e profissional.
        Dá respostas claras, diretas e bem organizadas.
        Se a pergunta for simples, responde de forma curta.
        Se a pergunta for técnica ou complexa, explica passo a passo.
        Se não souberes algo com confiança, diz isso de forma honesta e sugere o próximo passo.
        Não inventes factos, fontes, resultados ou capacidades.
        Não escrevas raciocínio interno, análises de benchmark, avaliações, notas, nem blocos com etiquetas como "Query:", "Avaliação:", "Score:", "Analysis:" ou semelhantes.
        Responde apenas com a resposta final para o utilizador.
        Se o utilizador pedir ajuda com código, programação ou configuração, sê prático e focado na solução.
        """;
    private readonly string _chatId;
    private readonly List<string> _modelResolutionDiagnostics = new();
    private readonly List<string> _modelLoadDiagnostics = new();
    private ChatSession _currentChat;
    private string _currentMessage;
    private EffectiveInferenceSettings? _effectiveSettings;
    private InteractiveExecutor? _executor;
    private InferenceParams _inferenceParams;
    private LLama.ChatSession? _session;
    private bool _llamaReady;
    private string? _initErrorMessage;
    private bool _isDeveloperStatsVisible;
    private string _responseTimeText = "Aguardando";
    private string _tokenStatsText = "0";
    private string _contextStatsText = "0 / 0";
    private string _memoryUsageText = "0 MB";

    public ChatViewModel(string chatId)
    {
        _chatId = chatId;
        SendMessageCommand = new Command(async () => await SendMessage());

        try
        {
            InitLLama();
            _llamaReady = true;
        }
        catch (Exception ex)
        {
            _llamaReady = false;
            _initErrorMessage = BuildDetailedError(ex);
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
        LoadSession();
    }

    public ObservableCollection<Message> Messages { get; set; } = new();

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

    public ICommand SendMessageCommand { get; }

    private void InitLLama()
    {
#if MACCATALYST || IOS
        // Configure native library path before touching LLamaSharp types.
        NativeLibraryHelper.ConfigureNativeLibrary();
#endif
        var selectedModel = ModelConfig.SelectedModelPath;
        var modelFile = "llama-3.2-1b-instruct-q8_0.gguf";
        var modelPath = ResolveModelPath(selectedModel, modelFile, _modelResolutionDiagnostics);
        _modelLoadDiagnostics.Add($"ModelPath={modelPath}");
        _modelLoadDiagnostics.Add($"SelectedModel={selectedModel ?? "<null>"}");
        IsDeveloperStatsVisible = InferenceSettingsService.IsDeveloperStatsEnabled;

        if (!File.Exists(modelPath))
        {
            var detail = _modelResolutionDiagnostics.Count == 0
                ? string.Empty
                : $" Caminhos verificados: {string.Join(" | ", _modelResolutionDiagnostics)}";
            throw new FileNotFoundException($"Modelo .gguf não encontrado. Seleciona ou importa um modelo nas definições.{detail}");
        }

        var modelInfo = new FileInfo(modelPath);
        _modelLoadDiagnostics.Add("Exists=true");
        _modelLoadDiagnostics.Add($"FileSize={modelInfo.Length}");
        _modelLoadDiagnostics.Add($"LastWriteUtc={modelInfo.LastWriteTimeUtc:O}");

        _effectiveSettings = InferenceSettingsService.GetEffectiveSettings(modelPath);
        _modelLoadDiagnostics.Add($"Profile={_effectiveSettings.ProfileName}");
        _modelLoadDiagnostics.Add($"AutoMode={_effectiveSettings.IsAutomatic}");

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = _effectiveSettings.ContextSize,
            GpuLayerCount = _effectiveSettings.GpuLayerCount
        };
        _modelLoadDiagnostics.Add($"ContextSize={parameters.ContextSize}");
        _modelLoadDiagnostics.Add($"GpuLayerCount={parameters.GpuLayerCount}");

        LLamaWeights model;
        try
        {
            model = LLamaWeights.LoadFromFile(parameters);
            _modelLoadDiagnostics.Add("LoadFromFile=ok");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha em LLamaWeights.LoadFromFile. Diagnóstico: {string.Join(" | ", _modelLoadDiagnostics)}",
                ex);
        }

        LLamaContext context;
        try
        {
            context = model.CreateContext(parameters);
            _modelLoadDiagnostics.Add("CreateContext=ok");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha em model.CreateContext. Diagnóstico: {string.Join(" | ", _modelLoadDiagnostics)}",
                ex);
        }
        _executor = new InteractiveExecutor(context);

        _inferenceParams = new InferenceParams
        {
            MaxTokens = _effectiveSettings.MaxTokens,
            AntiPrompts = new List<string> { "User:","Query:" }
        };
    }

    private static string ResolveModelPath(string? selectedModelPath, string defaultModelFile, List<string>? diagnostics = null)
    {
        if (!string.IsNullOrWhiteSpace(selectedModelPath))
        {
            diagnostics?.Add($"Selected={selectedModelPath}");
            if (File.Exists(selectedModelPath))
                return selectedModelPath;
        }

        var appDataModelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        Directory.CreateDirectory(appDataModelsDir);
        var appDataModelPath = Path.Combine(appDataModelsDir, defaultModelFile);
        diagnostics?.Add($"AppData={appDataModelPath}");
        if (File.Exists(appDataModelPath))
            return appDataModelPath;

        if (TryCopyFromAppPackage($"AI model/{defaultModelFile}", appDataModelPath) ||
            TryCopyFromAppPackage(defaultModelFile, appDataModelPath))
        {
            return appDataModelPath;
        }

        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDir, "AI model", defaultModelFile),
            Path.Combine(baseDir, "gguf models", defaultModelFile),
            Path.Combine(baseDir, "modelos de ai", defaultModelFile),
            Path.Combine(baseDir, defaultModelFile),
            // MacCatalyst bundles resources under Contents/Resources
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "AI model", defaultModelFile)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "gguf models", defaultModelFile)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "modelos de ai", defaultModelFile)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", defaultModelFile))
        };

        var ancestorMatch = FindInAncestorFolders(baseDir, "AI model", defaultModelFile);
        if (!string.IsNullOrWhiteSpace(ancestorMatch))
            candidates.Insert(0, ancestorMatch);

        foreach (var path in candidates.Distinct())
        {
            diagnostics?.Add($"Candidate={path}");
            if (File.Exists(path))
                return TryCopyToAppData(path, appDataModelPath) ?? path;
        }

        // Last resort: keep original file name for clearer exception message
        diagnostics?.Add($"Fallback={defaultModelFile}");
        return defaultModelFile;
    }

    private static string? TryCopyToAppData(string sourcePath, string destPath)
    {
        try
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryCopyFromAppPackage(string assetPath, string destPath)
    {
        try
        {
            using var src = FileSystem.OpenAppPackageFileAsync(assetPath).GetAwaiter().GetResult();
            using var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            src.CopyTo(dest);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindInAncestorFolders(string startDir, string folderName, string fileName)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, folderName, fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }

    private static string BuildDetailedError(Exception ex)
    {
        var parts = new List<string>();
        Exception? current = ex;
        while (current != null)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }

        return string.Join(" | ", parts);
    }

    private async Task SendMessage()
    {
        if (!_llamaReady)
        {
            Messages.Add(new Message
            {
                IsUser = false,
                Text = string.IsNullOrWhiteSpace(_initErrorMessage)
                    ? "Modelo não está carregado. Seleciona um modelo .gguf válido."
                    : $"Modelo não está carregado. {_initErrorMessage}"
            });
            return;
        }

        if (_session == null)
        {
            Messages.Add(new Message
            {
                IsUser = false,
                Text = "A conversa ainda está a ser preparada. Tenta novamente dentro de um instante."
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        var userInput = CurrentMessage.Trim();

        // Adicionar mensagem do utilizador
        Messages.Add(new Message { Text = userInput, IsUser = true });
        CurrentMessage = string.Empty;

        await UpdateChatTitleIfNeededAsync(userInput);

        // Criar mensagem do bot imediatamente (vazia) para mostrar em tempo real
        var botMessage = new Message { Text = "", IsUser = false };
        Messages.Add(botMessage);

        var botReply = "";
        var timer = Stopwatch.StartNew();

        // Atualizar a mensagem em tempo real conforme os chunks chegam
        var updateCount = 0;
        await foreach (var text in _session!.ChatAsync(
                           new ChatHistory.Message(AuthorRole.User, userInput),
                           _inferenceParams))
        {
            botReply += text;
            updateCount++;

            // Limpar prefixos indesejados enquanto está escrevendo
            var cleanedReply = botReply.Replace("bob:", "", StringComparison.OrdinalIgnoreCase)
                .Replace("User:", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            // Atualizar o texto da mensagem em tempo real
            // A propriedade Text já notifica a UI automaticamente via INotifyPropertyChanged
            botMessage.Text = cleanedReply;

            // Fazer scroll a cada 3 chunks para não sobrecarregar a UI
            if (updateCount % 3 == 0) await Task.Delay(1); // Pequeno delay para permitir que a UI atualize
        }

        // Limpeza final
        botReply = botReply.Replace("bob:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("User:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        botMessage.Text = botReply;
        timer.Stop();

        // Guardar conversa atualizada
        await SaveSessionAsync();
        UpdateDeveloperStats(botReply, timer.Elapsed);
    }

    private async void LoadSession()
    {
        _currentChat = await ChatStorage.GetChatByIdAsync(_chatId);

        if (_currentChat == null)
        {
            // Caso não exista (backup)
            _currentChat = new ChatSession
            {
                Id = _chatId,
                Title = DefaultChatTitle
            };

            var allChats = await ChatStorage.LoadChatsAsync();
            allChats.Add(_currentChat);
            await ChatStorage.SaveChatsAsync(allChats);
        }

        // Carregar mensagens salvas na UI
        Messages.Clear();
        foreach (var msg in _currentChat.Messages)
            Messages.Add(new Message
            {
                Text = msg.Text,
                IsUser = msg.Role == "user"
            });

        RebuildSessionFromCurrentChat();
        RefreshDeveloperStats();
    }

    private async Task SaveSessionAsync()
    {
        var allChats = await ChatStorage.LoadChatsAsync();
        var existing = allChats.FirstOrDefault(c => c.Id == _chatId);

        if (existing != null)
            existing.Messages = Messages.Select(m => new ChatMessage
            {
                Role = m.IsUser ? "user" : "bot",
                Text = m.Text
            }).ToList();
        else
            allChats.Add(new ChatSession
            {
                Id = _chatId,
                Title = "Nova Conversa",
                Messages = Messages.Select(m => new ChatMessage
                {
                    Role = m.IsUser ? "user" : "bot",
                    Text = m.Text
                }).ToList()
            });

        await ChatStorage.SaveChatsAsync(allChats);
    }

    private void RefreshDeveloperStats()
    {
        if (_effectiveSettings == null)
            return;

        UpdateDeveloperStats(string.Empty, TimeSpan.Zero);
    }

    private void RebuildSessionFromCurrentChat()
    {
        if (_executor == null)
            return;

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.System, SystemPrompt);

        if (_currentChat?.Messages != null)
        {
            foreach (var message in _currentChat.Messages)
            {
                var role = string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
                    ? AuthorRole.User
                    : AuthorRole.Assistant;

                chatHistory.AddMessage(role, message.Text);
            }
        }

        _session = new LLama.ChatSession(_executor, chatHistory);
    }

    private void UpdateDeveloperStats(string botReply, TimeSpan elapsed)
    {
        if (_effectiveSettings == null)
            return;

        var responseTokens = EstimateTokenCount(botReply);
        var historyTokens = EstimateTokenCount(SystemPrompt);

        historyTokens += Messages.Sum(m => EstimateTokenCount(m.Text));

        ResponseTimeText = elapsed == TimeSpan.Zero
            ? "Aguardando"
            : $"{elapsed.TotalSeconds:0.00}s";
        TokenStatsText = responseTokens > 0 ? responseTokens.ToString() : "0";
        ContextStatsText = $"{historyTokens} / {_effectiveSettings.ContextSize}";
        MemoryUsageText = FormatBytes(Process.GetCurrentProcess().WorkingSet64);
    }

    private async Task UpdateChatTitleIfNeededAsync(string firstUserMessage)
    {
        if (_currentChat == null || !string.Equals(_currentChat.Title, DefaultChatTitle, StringComparison.Ordinal))
            return;

        var generatedTitle = GenerateTitleFromFirstMessage(firstUserMessage);
        if (string.IsNullOrWhiteSpace(generatedTitle) ||
            string.Equals(generatedTitle, DefaultChatTitle, StringComparison.Ordinal))
        {
            return;
        }

        _currentChat.Title = generatedTitle;
        await ChatStorage.UpdateChatTitleAsync(_chatId, generatedTitle);
    }

    private static string GenerateTitleFromFirstMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DefaultChatTitle;

        var cleaned = text.Trim();
        cleaned = cleaned.Replace("\r", " ").Replace("\n", " ");

        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ");

        const int maxLength = 40;
        if (cleaned.Length <= maxLength)
            return cleaned;

        return cleaned[..37].TrimEnd() + "...";
    }

    private static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var normalized = text.Trim();
        var byChars = (int)Math.Ceiling(normalized.Length / 4d);
        var byWords = normalized.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

        return Math.Max(1, Math.Max(byChars, byWords));
    }

    private static string FormatBytes(long bytes)
    {
        const double mb = 1024d * 1024d;
        const double gb = mb * 1024d;

        if (bytes >= gb)
            return $"{bytes / gb:0.00} GB";

        return $"{bytes / mb:0.0} MB";
    }
}
