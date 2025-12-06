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

                // Encontrar o próximo ID disponível
                var countCommand = new SqliteCommand(
                    "SELECT COUNT(*) FROM ChatSessions",
                    connection);
                var count = Convert.ToInt32(countCommand.ExecuteScalar());
                int nextId = count + 1;
                string newId = $"chat{nextId}";

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
    }
}