using System.Security.Cryptography;
using System.Text;

namespace chatbot.Services;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
