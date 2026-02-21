using chatbot.Services;
using Microsoft.Data.Sqlite;

namespace chatbot;

public partial class SignupPage : ContentPage
{
    public SignupPage()
    {
        InitializeComponent();
    }

    private async void OnSignupClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();
        var confirm = ConfirmPasswordEntry.Text?.Trim();

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

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            await DisplayAlert("Password", "As passwords não coincidem.", "OK");
            return;
        }

        try
        {
            var signupResult = await Task.Run(() =>
            {
                using var connection = DatabaseHelper.GetConnection();
                var checkCommand = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Username = @Username", connection);
                checkCommand.Parameters.AddWithValue("@Username", username);

                if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
                    return (success: false, message: "Username já existe.", key: (byte[]?)null);

                var salt = new byte[16];
                Random.Shared.NextBytes(salt);

                var insertCommand = new SqliteCommand(
                    "INSERT INTO Users (Username, PasswordHash, Salt) VALUES (@Username, @PasswordHash, @Salt)",
                    connection);
                insertCommand.Parameters.AddWithValue("@Username", username);
                insertCommand.Parameters.AddWithValue("@PasswordHash", PasswordHasher.HashPassword(password));
                insertCommand.Parameters.AddWithValue("@Salt", Convert.ToBase64String(salt));
                insertCommand.ExecuteNonQuery();

                var key = PasswordKeyDeriver.DeriveKey(password, salt, 32);
                return (success: true, message: "", key: key);
            });

            if (!signupResult.success)
            {
                await DisplayAlert("Registo", signupResult.message, "OK");
                return;
            }

            UserContext.Set(username, signupResult.key!);
            Application.Current!.MainPage = new NavigationPage(new FrontPage());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível registar o utilizador. {ex.Message}", "OK");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
