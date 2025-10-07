using System.Collections.ObjectModel;
using System.Windows.Input;
using LLama;
using LLama.Common;
using chatbot.Models;
using chatbot.Services;

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
        string modelPath = @"llama-3.2-1b-instruct-q8_0.gguf";

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

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        // Adicionar mensagem do utilizador
        Messages.Add(new Message { Text = CurrentMessage, IsUser = true });

        string userInput = CurrentMessage;
        CurrentMessage = string.Empty;

        string botReply = "";

        await foreach (var text in _session.ChatAsync(
            new ChatHistory.Message(AuthorRole.User, userInput),
            _inferenceParams))
        {
            botReply += text;
        }

        botReply = botReply.Replace("bob:", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("User:", "", StringComparison.OrdinalIgnoreCase)
                           .Trim();

        // Adicionar resposta do bot
        Messages.Add(new Message { Text = botReply, IsUser = false });

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
