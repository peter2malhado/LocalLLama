using System.Collections.ObjectModel;
using System.Windows.Input;
using localllama;
using localllama.Models;
using localllama.Services;
using LLama;
using LLama.Common;
using ChatSession = localllama.Models.ChatSession;


public class ChatViewModel : BindableObject
{
    private readonly string _chatId;
    private readonly List<string> _modelResolutionDiagnostics = new();
    private readonly List<string> _modelLoadDiagnostics = new();
    private ChatSession _currentChat;
    private string _currentMessage;
    private InferenceParams _inferenceParams;
    private LLama.ChatSession _session;
    private bool _llamaReady;
    private string? _initErrorMessage;

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

    public string CurrentMessage
    {
        get => _currentMessage;
        set
        {
            _currentMessage = value;
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

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 1024,
            GpuLayerCount = (DeviceInfo.Platform == DevicePlatform.iOS) ? 0 : 5
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
        var executor = new InteractiveExecutor(context);

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.System,
            "Transcrição de uma caixa de diálogo, onde o Usuário interage com um Assistente chamado Bob. Bob é prestativo, gentil, honesto, bom em escrever e responde com clareza.");
        _session = new LLama.ChatSession(executor, chatHistory);

        _inferenceParams = new InferenceParams
        {
            MaxTokens = 256,
            AntiPrompts = new List<string> { "User:" }
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

        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        // Adicionar mensagem do utilizador
        Messages.Add(new Message { Text = CurrentMessage, IsUser = true });

        var userInput = CurrentMessage;
        CurrentMessage = string.Empty;

        // Criar mensagem do bot imediatamente (vazia) para mostrar em tempo real
        var botMessage = new Message { Text = "", IsUser = false };
        Messages.Add(botMessage);

        var botReply = "";

        // Atualizar a mensagem em tempo real conforme os chunks chegam
        var updateCount = 0;
        await foreach (var text in _session.ChatAsync(
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

        // Guardar conversa atualizada
        await SaveSessionAsync();
    }

    private async void LoadSession()
    {
        var allChats = await ChatStorage.LoadChatsAsync();
        _currentChat = allChats.FirstOrDefault(c => c.Id == _chatId);

        if (_currentChat == null)
        {
            // Caso não exista (backup)
            _currentChat = new ChatSession
            {
                Id = _chatId,
                Title = "Nova Conversa"
            };
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
}
