using Microsoft.Data.Sqlite;

namespace chatbot.Services;

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

    public static string GetDatabasePath()
    {
        return DatabasePath;
    }

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
                    UserId TEXT,
                    Title TEXT NOT NULL
                );";

        // Criar tabela de mensagens
        var createMessagesTable = @"
                CREATE TABLE IF NOT EXISTS ChatMessages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChatId TEXT NOT NULL,
                    UserId TEXT,
                    Role TEXT NOT NULL,
                    Text TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ChatId) REFERENCES ChatSessions(Id) ON DELETE CASCADE
                );";

        // Criar índices para melhor performance
        var createIndex = @"
                CREATE INDEX IF NOT EXISTS idx_ChatMessages_ChatId 
                ON ChatMessages(ChatId);";

        // Criar tabela de utilizadores
        var createUsersTable = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Username TEXT PRIMARY KEY,
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

        using var command1 = new SqliteCommand(createSessionsTable, connection);
        command1.ExecuteNonQuery();

        using var command2 = new SqliteCommand(createMessagesTable, connection);
        command2.ExecuteNonQuery();

        using var command3 = new SqliteCommand(createIndex, connection);
        command3.ExecuteNonQuery();

        using var command4 = new SqliteCommand(createUsersTable, connection);
        command4.ExecuteNonQuery();

        // Migrações leves (best-effort)
        TryExec(connection, "ALTER TABLE ChatSessions ADD COLUMN UserId TEXT;");
        TryExec(connection, "ALTER TABLE ChatMessages ADD COLUMN UserId TEXT;");
        TryExec(connection, "ALTER TABLE Users ADD COLUMN Salt TEXT;");
        TryExec(connection, "CREATE INDEX IF NOT EXISTS idx_ChatSessions_UserId ON ChatSessions(UserId);");
        TryExec(connection, "CREATE INDEX IF NOT EXISTS idx_ChatMessages_UserId ON ChatMessages(UserId);");
        TryExec(connection, "UPDATE ChatSessions SET UserId = 'default' WHERE UserId IS NULL;");
        TryExec(connection, "UPDATE ChatMessages SET UserId = 'default' WHERE UserId IS NULL;");
    }

    private static void TryExec(SqliteConnection connection, string sql)
    {
        try
        {
            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignorar erros de migração (coluna já existe, etc.)
        }
    }
}
