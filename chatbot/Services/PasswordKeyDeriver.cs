using System.Security.Cryptography;

namespace chatbot.Services;

public static class PasswordKeyDeriver
{
    public static byte[] DeriveKey(string password, byte[] salt, int bytes = 32)
    {
        if (string.IsNullOrEmpty(password)) return Array.Empty<byte>();
        return new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256).GetBytes(bytes);
    }
}
