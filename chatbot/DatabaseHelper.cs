using Microsoft.Data.Sqlite;

namespace chatbot.Services;

public static class DatabaseHelper
{
    private const string AuthDbName = "auth.db";

    private static string UserDatabasePath
    {
        get
        {
            DatabaseConfig.EnsureDatabaseLayout();
            return DatabaseConfig.GetSelectedDatabasePath();
        }
    }

    private static string AuthDatabasePath
    {
        get
        {
            Directory.CreateDirectory(DatabaseConfig.DatabasesDirectory);
            return Path.Combine(DatabaseConfig.DatabasesDirectory, AuthDbName);
        }
    }

    public static string GetDatabasePath()
    {
        return UserDatabasePath;
    }

    public static SqliteConnection GetUserConnection()
    {
        var connection = new SqliteConnection($"Data Source={UserDatabasePath}");
        connection.Open();
        return connection;
    }

    public static SqliteConnection GetAuthConnection()
    {
        var connection = new SqliteConnection($"Data Source={AuthDatabasePath}");
        connection.Open();
        return connection;
    }

    public static void InitializeAuthDatabase()
    {
        using var connection = GetAuthConnection();

        // Criar tabela de utilizadores
        var createUsersTable = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Username TEXT PRIMARY KEY,
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

        using var command = new SqliteCommand(createUsersTable, connection);
        command.ExecuteNonQuery();
    }

    public static void InitializeUserDatabase()
    {
        using var connection = GetUserConnection();

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

        using var command1 = new SqliteCommand(createSessionsTable, connection);
        command1.ExecuteNonQuery();

        using var command2 = new SqliteCommand(createMessagesTable, connection);
        command2.ExecuteNonQuery();

        using var command3 = new SqliteCommand(createIndex, connection);
        command3.ExecuteNonQuery();
    }
}
