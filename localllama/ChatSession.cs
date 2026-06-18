namespace localllama.Models;

public class ChatMessage
{
    public string Role { get; set; } // "user" ou "bot"
    public string Text { get; set; }
    public string? ImagePath { get; set; }
}

public class ChatSession
{
    public string Id { get; set; } // Ex: "chat1"
    public string Title { get; set; } // Ex: "Conversa sobre música"
    public string PersonalityName { get; set; } = Services.ChatPromptCatalog.DefaultPersonalityName;
    public string PersonalityPrompt { get; set; } = Services.ChatPromptCatalog.SystemPrompt;
    public List<ChatMessage> Messages { get; set; } = new();

    // Propriedade para exibir o número de mensagens
    public int MessageCount => Messages?.Count ?? 0;

    // Propriedade para exibir a última mensagem (preview)
    public string LastMessagePreview
    {
        get
        {
            if (Messages == null || Messages.Count == 0)
                return "Nenhuma mensagem ainda";

            var lastMessage = Messages.Last();
            if (!string.IsNullOrWhiteSpace(lastMessage.ImagePath))
                return string.IsNullOrWhiteSpace(lastMessage.Text) ? "[Foto]" : "[Foto] " + lastMessage.Text;

            var preview = lastMessage.Text ?? "";

            // Limitar a 50 caracteres para preview
            if (preview.Length > 50)
                preview = preview.Substring(0, 47) + "...";

            return preview;
        }
    }

    // Propriedade para indicar se tem mensagens
    public bool HasMessages => Messages != null && Messages.Count > 0;
}
