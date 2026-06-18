using System.Collections.ObjectModel;
using localllama.Models;
using ChatSessionModel = localllama.Models.ChatSession;

namespace localllama.Services;

public class ChatConversationService
{
    public async Task<ChatSessionModel> LoadOrCreateAsync(string chatId)
    {
        var chat = await ChatStorage.GetChatByIdAsync(chatId);
        if (chat != null)
            return chat;

        var newChat = new ChatSessionModel
        {
            Id = chatId,
            Title = ChatPromptCatalog.DefaultChatTitle,
            PersonalityName = PersonalitySelectionService.Selected.Name,
            PersonalityPrompt = PersonalitySelectionService.Selected.Prompt
        };

        var allChats = await ChatStorage.LoadChatsAsync();
        allChats.Add(newChat);
        await ChatStorage.SaveChatsAsync(allChats);
        return newChat;
    }

    public void PopulateMessages(ObservableCollection<Message> target, IEnumerable<ChatMessage> source)
    {
        target.Clear();
        foreach (var msg in source)
        {
            target.Add(new Message
            {
                Text = msg.Text,
                IsUser = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase),
                ImagePath = msg.ImagePath
            });
        }
    }

    public async Task SaveAsync(
        string chatId,
        string title,
        IEnumerable<Message> messages,
        string? personalityName = null,
        string? personalityPrompt = null)
    {
        var allChats = await ChatStorage.LoadChatsAsync();
        var existing = allChats.FirstOrDefault(c => c.Id == chatId);
        var mappedMessages = messages.Select(m => new ChatMessage
        {
            Role = m.IsUser ? "user" : "bot",
            Text = m.Text,
            ImagePath = m.ImagePath
        }).ToList();

        if (existing != null)
        {
            existing.Title = title;
            existing.PersonalityName = personalityName ?? existing.PersonalityName;
            existing.PersonalityPrompt = personalityPrompt ?? existing.PersonalityPrompt;
            existing.Messages = mappedMessages;
        }
        else
        {
            allChats.Add(new ChatSessionModel
            {
                Id = chatId,
                Title = title,
                PersonalityName = personalityName ?? PersonalitySelectionService.Selected.Name,
                PersonalityPrompt = personalityPrompt ?? PersonalitySelectionService.Selected.Prompt,
                Messages = mappedMessages
            });
        }

        await ChatStorage.SaveChatsAsync(allChats);
    }

    public async Task<string?> UpdateTitleIfNeededAsync(ChatSessionModel? chat, string chatId, string firstUserMessage)
    {
        if (chat == null || !string.Equals(chat.Title, ChatPromptCatalog.DefaultChatTitle, StringComparison.Ordinal))
            return null;

        var generatedTitle = GenerateTitleFromFirstMessage(firstUserMessage);
        if (string.IsNullOrWhiteSpace(generatedTitle) ||
            string.Equals(generatedTitle, ChatPromptCatalog.DefaultChatTitle, StringComparison.Ordinal))
        {
            return null;
        }

        chat.Title = generatedTitle;
        await ChatStorage.UpdateChatTitleAsync(chatId, generatedTitle);
        return generatedTitle;
    }

    private static string GenerateTitleFromFirstMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ChatPromptCatalog.DefaultChatTitle;

        var cleaned = text.Trim().Replace("\r", " ").Replace("\n", " ");
        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ");

        const int maxLength = 40;
        return cleaned.Length <= maxLength ? cleaned : cleaned[..37].TrimEnd() + "...";
    }
}
