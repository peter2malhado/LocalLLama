using Microsoft.Data.Sqlite;

namespace chatbot.Services
{
    public static class DatabaseHelper
    {
        private static string DatabasePath
        {
            get
            {
                DatabaseConfig.EnsureDatabaseLayout();
                return DatabaseConfig.GetSelectedDatabasePath();
            }
        }

        public static string GetDatabasePath() => DatabasePath;

        public static SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection($"Data Source={DatabasePath}");
            connection.Open();
            return connection;
        }

        public static void InitializeDatabase()
        {
            using var connection = GetConnection();

            // Criar tabela de sessões de chat
            var createSessionsTable = @"
                CREATE TABLE IF NOT EXISTS ChatSessions (
                    Id TEXT PRIMARY KEY,
                    Title TEXT NOT NULL
                );";

            // Criar tabela de mensagens
            var createMessagesTable = @"
                CREATE TABLE IF NOT EXISTS ChatMessages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChatId TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    Text TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ChatId) REFERENCES ChatSessions(Id) ON DELETE CASCADE
                );";

            // Criar índices para melhor performance
            var createIndex = @"
                CREATE INDEX IF NOT EXISTS idx_ChatMessages_ChatId 
                ON ChatMessages(ChatId);";

            using var command1 = new SqliteCommand(createSessionsTable, connection);
            command1.ExecuteNonQuery();

            using var command2 = new SqliteCommand(createMessagesTable, connection);
            command2.ExecuteNonQuery();

            using var command3 = new SqliteCommand(createIndex, connection);
            command3.ExecuteNonQuery();
        }
    }
}
