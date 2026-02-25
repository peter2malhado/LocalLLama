using System.Security.Cryptography;
using System.Text;

namespace chatbot.Services;

public static class ChatCrypto
{
    private const string Prefix = "enc:v1:";

    public static string EncryptText(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        if (IsEncrypted(plainText)) return plainText;

        var key = UserContext.EncryptionKey;
        if (key == null || key.Length != 32)
            return plainText;

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public static string DecryptText(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (!IsEncrypted(value)) return value;

        var key = UserContext.EncryptionKey;
        if (key == null || key.Length != 32)
            return value;

        try
        {
            var payload = Convert.FromBase64String(value[Prefix.Length..]);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;

            var ivLength = aes.BlockSize / 8;
            if (payload.Length <= ivLength) return value;

            var iv = new byte[ivLength];
            Buffer.BlockCopy(payload, 0, iv, 0, ivLength);
            var cipherBytes = new byte[payload.Length - ivLength];
            Buffer.BlockCopy(payload, ivLength, cipherBytes, 0, cipherBytes.Length);

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Se não conseguir desencriptar, retorna o valor original.
            return value;
        }
    }

    private static bool IsEncrypted(string value) =>
        value.StartsWith(Prefix, StringComparison.Ordinal);
}
