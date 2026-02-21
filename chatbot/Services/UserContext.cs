namespace chatbot.Services;

public static class UserContext
{
    public static string? Username { get; private set; }
    public static byte[]? EncryptionKey { get; private set; }

    public static bool IsReady => !string.IsNullOrWhiteSpace(Username) && EncryptionKey != null;

    public static void Set(string username, byte[] encryptionKey)
    {
        Username = username;
        EncryptionKey = encryptionKey;
    }

    public static void Clear()
    {
        Username = null;
        EncryptionKey = null;
    }
}
