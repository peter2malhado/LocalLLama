using chatbot.Services;
using Microsoft.Data.Sqlite;

namespace chatbot;

public partial class  LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrEmpty(username))
        {
            await DisplayAlert("Username", "Por favor, introduz o username.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            await DisplayAlert("Password", "Por favor, introduz a password.", "OK");
            return;
        }

        try
        {
            var loginResult = await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetAuthConnection();
                var command = new SqliteCommand(
                    "SELECT PasswordHash, Salt FROM Users WHERE Username = @Username",
                    connection);
                command.Parameters.AddWithValue("@Username", username);

                using var reader = command.ExecuteReader();
                if (!reader.Read())
                    return (success: false, message: "Utilizador não encontrado. Usa 'Registar'.", key: (byte[]?)null);

                var storedHash = reader.GetString(0);
                var storedSalt = reader.IsDBNull(1) ? null : reader.GetString(1);
                var enteredHash = PasswordHasher.HashPassword(password);

                if (!string.Equals(storedHash, enteredHash, StringComparison.Ordinal))
                    return (success: false, message: "Password incorreta.", key: (byte[]?)null);

                var saltBytes = EnsureUserSalt(connection, username, storedSalt);
                var key = PasswordKeyDeriver.DeriveKey(password, saltBytes, 32);
                return (success: true, message: "", key: key);
            });

            if (!loginResult.success)
            {
                await DisplayAlert("Login", loginResult.message, "OK");
                return;
            }

            UserContext.Set(username, loginResult.key!);
            DatabaseConfig.SelectedDatabaseName = $"{username}.db";
            DatabaseHelper.InitializeUserDatabase();
            DatabaseHelper.InitializeRagDatabase();
            Application.Current!.MainPage = new NavigationPage(new FrontPage());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível validar a password. {ex.Message}", "OK");
        }
        finally
        {
            PasswordEntry.Text = string.Empty;
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SignupPage());
    }

    private static byte[] EnsureUserSalt(SqliteConnection connection, string username, string? storedSalt)
    {
        if (!string.IsNullOrWhiteSpace(storedSalt))
            return Convert.FromBase64String(storedSalt);

        var salt = new byte[16];
        Random.Shared.NextBytes(salt);

        var update = new SqliteCommand(
            "UPDATE Users SET Salt = @Salt WHERE Username = @Username",
            connection);
        update.Parameters.AddWithValue("@Salt", Convert.ToBase64String(salt));
        update.Parameters.AddWithValue("@Username", username);
        update.ExecuteNonQuery();

        return salt;
    }
}
