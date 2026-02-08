using chatbot.Models;
using chatbot.Services;
using LLama;
using LLama.Common;
using System.Collections.ObjectModel;
using System.Windows.Input;
using chatbot;
using LLama.Native;

public class ChatViewModel : BindableObject
{
    private string _currentMessage;
    private LLama.ChatSession _session;
    private InferenceParams _inferenceParams;
    private chatbot.Models.ChatSession _currentChat;

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

    private readonly string _chatId;

    public ChatViewModel(string chatId)
    {

        _chatId = chatId;
        SendMessageCommand = new Command(async () => await SendMessage());

        InitLLama();
        LoadSession();
    }

    private void InitLLama()
    {
#if MACCATALYST || IOS
        // Configure native library path before touching LLamaSharp types.
        NativeLibraryHelper.ConfigureNativeLibrary();
#endif
        var selectedModel = ModelConfig.SelectedModelPath;
        string modelFile = "Jan-v3-4b-base-instruct-Q8_0.gguf";
        string modelPath = ResolveModelPath(selectedModel, modelFile);

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 1024,
            GpuLayerCount = 5
        };

        var model = LLamaWeights.LoadFromFile(parameters);
        var context = model.CreateContext(parameters);
        var executor = new InteractiveExecutor(context);

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.System,
            "Transcrição de uma caixa de diálogo, onde o Usuário interage com um Assistente chamado Bob. Bob é prestativo, gentil, honesto, bom em escrever e responde com clareza.");
        _session = new LLama.ChatSession(executor, chatHistory);

        _inferenceParams = new InferenceParams()
        {
            MaxTokens = 256,
            AntiPrompts = new List<string> { "User:" }
        };
    }

    private static string ResolveModelPath(string? selectedModelPath, string defaultModelFile)
    {
        if (!string.IsNullOrWhiteSpace(selectedModelPath) && File.Exists(selectedModelPath))
        {
            return selectedModelPath;
        }

        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDir, "modelos de ai", defaultModelFile),
            Path.Combine(baseDir, defaultModelFile),
            // MacCatalyst bundles resources under Contents/Resources
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "modelos de ai", defaultModelFile)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", defaultModelFile)),
        };

        foreach (var path in candidates.Distinct())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Last resort: keep original file name for clearer exception message
        return defaultModelFile;
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        // Adicionar mensagem do utilizador
        Messages.Add(new Message { Text = CurrentMessage, IsUser = true });

        string userInput = CurrentMessage;
        CurrentMessage = string.Empty;

        // Criar mensagem do bot imediatamente (vazia) para mostrar em tempo real
        var botMessage = new Message { Text = "", IsUser = false };
        Messages.Add(botMessage);

        string botReply = "";

        // Atualizar a mensagem em tempo real conforme os chunks chegam
        int updateCount = 0;
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
            if (updateCount % 3 == 0)
            {
                await Task.Delay(1); // Pequeno delay para permitir que a UI atualize
            }
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
            _currentChat = new chatbot.Models.ChatSession
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
        {
            Messages.Add(new Message
            {
                Text = msg.Text,
                IsUser = msg.Role == "user"
            });
        }
    }

    private async Task SaveSessionAsync()
    {
        var allChats = await ChatStorage.LoadChatsAsync();
        var existing = allChats.FirstOrDefault(c => c.Id == _chatId);

        if (existing != null)
        {
            existing.Messages = Messages.Select(m => new ChatMessage
            {
                Role = m.IsUser ? "user" : "bot",
                Text = m.Text
            }).ToList();
        }
        else
        {
            allChats.Add(new chatbot.Models.ChatSession
            {
                Id = _chatId,
                Title = "Nova Conversa",
                Messages = Messages.Select(m => new ChatMessage
                {
                    Role = m.IsUser ? "user" : "bot",
                    Text = m.Text
                }).ToList()
            });
        }

        await ChatStorage.SaveChatsAsync(allChats);
    }
}
