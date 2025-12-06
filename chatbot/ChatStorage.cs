using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using chatbot.Models;

namespace chatbot.Services
{
    public static class ChatStorage
    {
        static ChatStorage()
        {
            // Inicializar o banco de dados na primeira vez
            DatabaseHelper.InitializeDatabase();
        }

        // 🚀 Carrega todos os chats
        public static async Task<List<ChatSession>> LoadChatsAsync()
        {
            return await Task.Run(() =>
            {
                var chats = new List<ChatSession>();

                using var connection = DatabaseHelper.GetConnection();

                // Carregar todas as sessões primeiro
                var selectSessionsCommand = new SqliteCommand(
                    "SELECT Id, Title FROM ChatSessions ORDER BY Id",
                    connection);

                var sessionIds = new List<string>();
                using (var reader = selectSessionsCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var chat = new ChatSession
                        {
                            Id = reader.GetString(0),
                            Title = reader.GetString(1),
                            Messages = new List<ChatMessage>()
                        };
                        chats.Add(chat);
                        sessionIds.Add(chat.Id);
                    }
                }

                // Carregar todas as mensagens de uma vez usando JOIN
                if (sessionIds.Count > 0)
                {
                    var placeholders = string.Join(",", sessionIds.Select((_, i) => $"@id{i}"));
                    var selectMessagesCommand = new SqliteCommand(
                        $"SELECT ChatId, Role, Text FROM ChatMessages WHERE ChatId IN ({placeholders}) ORDER BY ChatId, Id",
                        connection);

                    for (int i = 0; i < sessionIds.Count; i++)
                    {
                        selectMessagesCommand.Parameters.AddWithValue($"@id{i}", sessionIds[i]);
                    }

                    using var messagesReader = selectMessagesCommand.ExecuteReader();
                    var messagesByChatId = new Dictionary<string, List<ChatMessage>>();

                    while (messagesReader.Read())
                    {
                        var chatId = messagesReader.GetString(0);
                        if (!messagesByChatId.ContainsKey(chatId))
                        {
                            messagesByChatId[chatId] = new List<ChatMessage>();
                        }

                        messagesByChatId[chatId].Add(new ChatMessage
                        {
                            Role = messagesReader.GetString(1),
                            Text = messagesReader.GetString(2)
                        });
                    }

                    // Associar mensagens aos chats
                    foreach (var chat in chats)
                    {
                        if (messagesByChatId.ContainsKey(chat.Id))
                        {
                            chat.Messages = messagesByChatId[chat.Id];
                        }
                    }
                }

                return chats;
            });
        }

        // 💾 Salva todos os chats (mantido para compatibilidade, mas agora usa SQLite)
        public static async Task SaveChatsAsync(List<ChatSession> chats)
        {
            await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetConnection();

                foreach (var chat in chats)
                {
                    // Inserir ou atualizar sessão
                    var upsertSessionCommand = new SqliteCommand(
                        @"INSERT OR REPLACE INTO ChatSessions (Id, Title) 
                          VALUES (@Id, @Title)",
                        connection);
                    upsertSessionCommand.Parameters.AddWithValue("@Id", chat.Id);
                    upsertSessionCommand.Parameters.AddWithValue("@Title", chat.Title);
                    upsertSessionCommand.ExecuteNonQuery();

                    // Limpar mensagens antigas e inserir novas
                    var deleteMessagesCommand = new SqliteCommand(
                        "DELETE FROM ChatMessages WHERE ChatId = @ChatId",
                        connection);
                    deleteMessagesCommand.Parameters.AddWithValue("@ChatId", chat.Id);
                    deleteMessagesCommand.ExecuteNonQuery();

                    // Inserir mensagens
                    foreach (var message in chat.Messages)
                    {
                        var insertMessageCommand = new SqliteCommand(
                            "INSERT INTO ChatMessages (ChatId, Role, Text) VALUES (@ChatId, @Role, @Text)",
                            connection);
                        insertMessageCommand.Parameters.AddWithValue("@ChatId", chat.Id);
                        insertMessageCommand.Parameters.AddWithValue("@Role", message.Role);
                        insertMessageCommand.Parameters.AddWithValue("@Text", message.Text);
                        insertMessageCommand.ExecuteNonQuery();
                    }
                }
            });
        }

        // ➕ Cria um novo chat com ID automático
        public static async Task<ChatSession> CreateNewChatAsync(string title = "Nova Conversa")
        {
            return await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetConnection();

                // Encontrar o próximo ID disponível verificando IDs existentes
                string newId = string.Empty; // Inicialização necessária
                int attempt = 1;
                bool idExists = true;

                // Tentar encontrar um ID único
                while (idExists && attempt < 10000) // Limite de segurança
                {
                    newId = $"chat{attempt}";
                    
                    var checkCommand = new SqliteCommand(
                        "SELECT COUNT(*) FROM ChatSessions WHERE Id = @Id",
                        connection);
                    checkCommand.Parameters.AddWithValue("@Id", newId);
                    
                    var count = Convert.ToInt32(checkCommand.ExecuteScalar());
                    idExists = count > 0;
                    
                    if (!idExists)
                    {
                        break;
                    }
                    
                    attempt++;
                }

                // Se ainda não encontrou um ID único, usar GUID como fallback
                if (idExists)
                {
                    newId = $"chat_{Guid.NewGuid().ToString("N")[..8]}";
                }

                // Inserir nova sessão
                var insertCommand = new SqliteCommand(
                    "INSERT INTO ChatSessions (Id, Title) VALUES (@Id, @Title)",
                    connection);
                insertCommand.Parameters.AddWithValue("@Id", newId);
                insertCommand.Parameters.AddWithValue("@Title", title);
                insertCommand.ExecuteNonQuery();

                return new ChatSession
                {
                    Id = newId,
                    Title = title,
                    Messages = new List<ChatMessage>()
                };
            });
        }

        // 🔍 Obter chat por ID
        public static async Task<ChatSession?> GetChatByIdAsync(string id)
        {
            return await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetConnection();

                // Buscar sessão
                var selectSessionCommand = new SqliteCommand(
                    "SELECT Id, Title FROM ChatSessions WHERE Id = @Id",
                    connection);
                selectSessionCommand.Parameters.AddWithValue("@Id", id);

                using var reader = selectSessionCommand.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                var chat = new ChatSession
                {
                    Id = reader.GetString(0),
                    Title = reader.GetString(1),
                    Messages = new List<ChatMessage>()
                };

                // Buscar mensagens
                var selectMessagesCommand = new SqliteCommand(
                    "SELECT Role, Text FROM ChatMessages WHERE ChatId = @ChatId ORDER BY Id",
                    connection);
                selectMessagesCommand.Parameters.AddWithValue("@ChatId", id);

                using var messagesReader = selectMessagesCommand.ExecuteReader();
                while (messagesReader.Read())
                {
                    chat.Messages.Add(new ChatMessage
                    {
                        Role = messagesReader.GetString(0),
                        Text = messagesReader.GetString(1)
                    });
                }

                return chat;
            });
        }

        // 📝 Adicionar mensagem a uma conversa
        public static async Task AddMessageToChatAsync(string chatId, string role, string text)
        {
            await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetConnection();

                // Verificar se a sessão existe
                var checkCommand = new SqliteCommand(
                    "SELECT COUNT(*) FROM ChatSessions WHERE Id = @Id",
                    connection);
                checkCommand.Parameters.AddWithValue("@Id", chatId);

                if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
                {
                    // Inserir mensagem
                    var insertCommand = new SqliteCommand(
                        "INSERT INTO ChatMessages (ChatId, Role, Text) VALUES (@ChatId, @Role, @Text)",
                        connection);
                    insertCommand.Parameters.AddWithValue("@ChatId", chatId);
                    insertCommand.Parameters.AddWithValue("@Role", role);
                    insertCommand.Parameters.AddWithValue("@Text", text);
                    insertCommand.ExecuteNonQuery();
                }
            });
        }

        // ✏️ Atualizar título de uma conversa
        public static async Task UpdateChatTitleAsync(string chatId, string newTitle)
        {
            await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetConnection();

                var updateCommand = new SqliteCommand(
                    "UPDATE ChatSessions SET Title = @Title WHERE Id = @Id",
                    connection);
                updateCommand.Parameters.AddWithValue("@Id", chatId);
                updateCommand.Parameters.AddWithValue("@Title", newTitle);
                updateCommand.ExecuteNonQuery();
            });
        }

        // 🗑️ Deletar uma conversa e todas as suas mensagens
        public static async Task DeleteChatAsync(string chatId)
        {
            await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetConnection();

                // Deletar mensagens primeiro (devido à foreign key)
                var deleteMessagesCommand = new SqliteCommand(
                    "DELETE FROM ChatMessages WHERE ChatId = @ChatId",
                    connection);
                deleteMessagesCommand.Parameters.AddWithValue("@ChatId", chatId);
                deleteMessagesCommand.ExecuteNonQuery();

                // Deletar a sessão
                var deleteSessionCommand = new SqliteCommand(
                    "DELETE FROM ChatSessions WHERE Id = @Id",
                    connection);
                deleteSessionCommand.Parameters.AddWithValue("@Id", chatId);
                deleteSessionCommand.ExecuteNonQuery();
            });
        }
    }
}