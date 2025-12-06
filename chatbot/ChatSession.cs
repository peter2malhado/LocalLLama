using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatbot.Models
{
    public class ChatMessage
    {
        public string Role { get; set; } // "user" ou "bot"
        public string Text { get; set; }
    }

    public class ChatSession
    {
        public string Id { get; set; }       // Ex: "chat1"
        public string Title { get; set; }    // Ex: "Conversa sobre música"
        public List<ChatMessage> Messages { get; set; } = new();
        
        // Propriedade para exibir o número de mensagens
        public int MessageCount => Messages?.Count ?? 0;
    }
}
