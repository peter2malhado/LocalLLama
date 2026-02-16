using Microsoft.Maui.Storage;

namespace chatbot.Services
{
    public static class DatabaseConfig
    {
        private const string SelectedDbKey = "selected_database_name";
        private const string DefaultDbName = "chats.db";

        public static string DatabasesDirectory =>
            Path.Combine(FileSystem.AppDataDirectory, "databases");

        public static string SelectedDatabaseName
        {
            get => Preferences.Get(SelectedDbKey, DefaultDbName);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Preferences.Remove(SelectedDbKey);
                    return;
                }

                Preferences.Set(SelectedDbKey, value);
            }
        }

        public static string GetSelectedDatabasePath()
        {
            Directory.CreateDirectory(DatabasesDirectory);
            return Path.Combine(DatabasesDirectory, SelectedDatabaseName);
        }

        public static void EnsureDatabaseLayout()
        {
            Directory.CreateDirectory(DatabasesDirectory);

            var selectedPath = GetSelectedDatabasePath();
            if (File.Exists(selectedPath))
                return;

            // Migrar base antiga se existir
            var legacyPath = Path.Combine(FileSystem.AppDataDirectory, DefaultDbName);
            if (File.Exists(legacyPath))
            {
                File.Move(legacyPath, selectedPath);
            }
        }
    }
}
