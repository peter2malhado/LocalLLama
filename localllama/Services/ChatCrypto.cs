using System.Security.Cryptography;
using System.Text;

namespace localllama.Services;

public static class ChatCrypto
{
    private const string Prefix = "enc:v1:";
    private static readonly byte[] BinaryPrefix = Encoding.UTF8.GetBytes(Prefix);

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
        var payload = EncryptBytesInternal(plainBytes, aes, encryptor);

        return Prefix + Convert.ToBase64String(payload);
    }

    public static byte[] EncryptBytes(byte[] plainBytes)
    {
        if (plainBytes == null || plainBytes.Length == 0) return plainBytes ?? Array.Empty<byte>();
        if (IsEncrypted(plainBytes)) return plainBytes;

        var key = UserContext.EncryptionKey;
        if (key == null || key.Length != 32)
            return plainBytes;

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var payload = EncryptBytesInternal(plainBytes, aes, encryptor);
        var encrypted = new byte[BinaryPrefix.Length + payload.Length];
        Buffer.BlockCopy(BinaryPrefix, 0, encrypted, 0, BinaryPrefix.Length);
        Buffer.BlockCopy(payload, 0, encrypted, BinaryPrefix.Length, payload.Length);
        return encrypted;
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
            var plainBytes = DecryptPayload(payload, key);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Se não conseguir desencriptar, retorna o valor original.
            return value;
        }
    }

    public static byte[] DecryptBytes(byte[] value)
    {
        if (value == null || value.Length == 0) return value ?? Array.Empty<byte>();
        if (!IsEncrypted(value)) return value;

        var key = UserContext.EncryptionKey;
        if (key == null || key.Length != 32)
            return value;

        try
        {
            var payload = new byte[value.Length - BinaryPrefix.Length];
            Buffer.BlockCopy(value, BinaryPrefix.Length, payload, 0, payload.Length);
            return DecryptPayload(payload, key);
        }
        catch
        {
            return value;
        }
    }

    private static bool IsEncrypted(string value) =>
        value.StartsWith(Prefix, StringComparison.Ordinal);

    private static bool IsEncrypted(byte[] value)
    {
        if (value.Length < BinaryPrefix.Length)
            return false;

        for (var i = 0; i < BinaryPrefix.Length; i++)
        {
            if (value[i] != BinaryPrefix[i])
                return false;
        }

        return true;
    }

    private static byte[] EncryptBytesInternal(byte[] plainBytes, Aes aes, ICryptoTransform encryptor)
    {
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);
        return payload;
    }

    private static byte[] DecryptPayload(byte[] payload, byte[] key)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;

        var ivLength = aes.BlockSize / 8;
        if (payload.Length <= ivLength)
            return payload;

        var iv = new byte[ivLength];
        Buffer.BlockCopy(payload, 0, iv, 0, ivLength);
        var cipherBytes = new byte[payload.Length - ivLength];
        Buffer.BlockCopy(payload, ivLength, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    }
}
