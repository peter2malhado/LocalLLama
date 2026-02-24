using Microsoft.Data.Sqlite;

namespace chatbot.Services;

public static class DatabaseHelper
{
    private const string AuthDbName = "auth.db";
    private const string RagDbName = "rag.db";

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

    private static string RagDatabasePath
    {
        get
        {
            Directory.CreateDirectory(DatabaseConfig.CurrentUserDirectory);
            return Path.Combine(DatabaseConfig.CurrentUserDirectory, RagDbName);
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

    public static SqliteConnection GetRagConnection()
    {
        var connection = new SqliteConnection($"Data Source={RagDatabasePath}");
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

    public static void InitializeRagDatabase()
    {
        using var connection = GetRagConnection();

        var createDocs = @"
                CREATE TABLE IF NOT EXISTS RagDocuments (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Path TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

        var createChunks = @"
                CREATE TABLE IF NOT EXISTS RagChunks (
                    Id TEXT PRIMARY KEY,
                    DocId TEXT NOT NULL,
                    ChunkIndex INTEGER NOT NULL,
                    TextEncrypted TEXT NOT NULL,
                    Embedding BLOB NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (DocId) REFERENCES RagDocuments(Id) ON DELETE CASCADE
                );";

        var createIndex = @"
                CREATE INDEX IF NOT EXISTS idx_RagChunks_DocId
                ON RagChunks(DocId);";

        using var c1 = new SqliteCommand(createDocs, connection);
        c1.ExecuteNonQuery();

        using var c2 = new SqliteCommand(createChunks, connection);
        c2.ExecuteNonQuery();

        using var c3 = new SqliteCommand(createIndex, connection);
        c3.ExecuteNonQuery();
    }
}
