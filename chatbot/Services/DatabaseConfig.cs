namespace chatbot.Services;

public static class DatabaseConfig
{
    private const string SelectedDbKey = "selected_database_name";
    private const string DefaultDbName = "chats.db";

    public static string DatabasesDirectory =>
        Path.Combine(FileSystem.AppDataDirectory, "databases");

    public static string? CurrentUserName => UserContext.Username;

    public static string CurrentUserDirectory
    {
        get
        {
            var user = CurrentUserName;
            if (string.IsNullOrWhiteSpace(user))
                return DatabasesDirectory;

            return Path.Combine(DatabasesDirectory, user);
        }
    }

    private static string SelectedDbKeyForUser =>
        string.IsNullOrWhiteSpace(CurrentUserName) ? SelectedDbKey : $"{SelectedDbKey}_{CurrentUserName}";

    public static string SelectedDatabaseName
    {
        get => Preferences.Get(SelectedDbKeyForUser, DefaultDbName);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Preferences.Remove(SelectedDbKeyForUser);
                return;
            }

            Preferences.Set(SelectedDbKeyForUser, value);
        }
    }

    public static string GetSelectedDatabasePath()
    {
        Directory.CreateDirectory(CurrentUserDirectory);
        return Path.Combine(CurrentUserDirectory, SelectedDatabaseName);
    }

    public static void EnsureDatabaseLayout()
    {
        Directory.CreateDirectory(DatabasesDirectory);
        Directory.CreateDirectory(CurrentUserDirectory);

        var selectedPath = GetSelectedDatabasePath();
        if (File.Exists(selectedPath))
            return;

        // Migrar base antiga se existir
        var legacyPath = Path.Combine(FileSystem.AppDataDirectory, DefaultDbName);
        if (File.Exists(legacyPath)) File.Move(legacyPath, selectedPath);
    }
}
