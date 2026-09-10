using System.Security.Cryptography;
using System.Text;

namespace BitwardenStreamdeckPlugin.Tests.Integration;

/// <summary>
/// Builds the registration payload for a Bitwarden compatible server.
///
/// There is no "bw register" command, so an end to end test has to create its account the
/// way the official clients do: derive the keys locally and post the result. The steps are
/// fixed by the Bitwarden protocol:
///
///   masterKey          = PBKDF2-SHA256(password, salt = lowercased email, iterations)
///   masterPasswordHash = PBKDF2-SHA256(masterKey, salt = password, 1 iteration)
///   stretched enc/mac  = HKDF-Expand(masterKey, "enc" / "mac")
///   protected key      = AES-256-CBC(random 64 byte symmetric key) + HMAC-SHA256
///
/// If any of this is wrong the follow up "bw login" simply fails, so the end to end test
/// validates this code as a side effect.
/// </summary>
internal static class BitwardenAccount
{
    internal const int KdfIterations = 600_000;

    internal static object BuildRegistrationRequest(string email, string password, string name)
    {
        byte[] masterKey = DeriveMasterKey(email, password);
        string masterPasswordHash = Convert.ToBase64String(DeriveMasterPasswordHash(masterKey, password));

        byte[] stretchedEncryptionKey = HkdfExpand(masterKey, "enc", 32);
        byte[] stretchedMacKey = HkdfExpand(masterKey, "mac", 32);

        // The account's own symmetric key: 32 bytes of encryption key + 32 bytes of MAC key.
        byte[] symmetricKey = RandomNumberGenerator.GetBytes(64);
        string protectedSymmetricKey = Encrypt(symmetricKey, stretchedEncryptionKey, stretchedMacKey);

        using RSA rsa = RSA.Create(2048);
        byte[] privateKey = rsa.ExportPkcs8PrivateKey();
        string publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        string encryptedPrivateKey = Encrypt(
            privateKey,
            symmetricKey.AsSpan(0, 32).ToArray(),
            symmetricKey.AsSpan(32, 32).ToArray());

        return new
        {
            email,
            name,
            masterPasswordHash,
            masterPasswordHint = (string?)null,
            key = protectedSymmetricKey,
            kdf = 0,
            kdfIterations = KdfIterations,
            keys = new
            {
                publicKey,
                encryptedPrivateKey
            }
        };
    }

    internal static byte[] DeriveMasterKey(string email, string password)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()),
            KdfIterations,
            HashAlgorithmName.SHA256,
            32);
    }

    internal static byte[] DeriveMasterPasswordHash(byte[] masterKey, string password)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            masterKey,
            Encoding.UTF8.GetBytes(password),
            1,
            HashAlgorithmName.SHA256,
            32);
    }

    /// <summary>
    /// HKDF expand only (the master key is already a pseudo random key, so no extract step).
    /// </summary>
    internal static byte[] HkdfExpand(byte[] prk, string info, int outputLength)
    {
        using var hmac = new HMACSHA256(prk);
        byte[] infoBytes = Encoding.UTF8.GetBytes(info);
        var output = new byte[outputLength];
        byte[] previousBlock = Array.Empty<byte>();
        int position = 0;

        for (byte counter = 1; position < outputLength; counter++)
        {
            var input = new byte[previousBlock.Length + infoBytes.Length + 1];
            previousBlock.CopyTo(input, 0);
            infoBytes.CopyTo(input, previousBlock.Length);
            input[^1] = counter;

            previousBlock = hmac.ComputeHash(input);
            int take = Math.Min(previousBlock.Length, outputLength - position);
            Array.Copy(previousBlock, 0, output, position, take);
            position += take;
        }

        return output;
    }

    /// <summary>
    /// Produces Bitwarden's "type 2" cipher string: AES-256-CBC then HMAC-SHA256 over iv||ct.
    /// </summary>
    internal static string Encrypt(byte[] plainText, byte[] encryptionKey, byte[] macKey)
    {
        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        byte[] cipherText = aes.EncryptCbc(plainText, aes.IV, PaddingMode.PKCS7);

        var macInput = new byte[aes.IV.Length + cipherText.Length];
        aes.IV.CopyTo(macInput, 0);
        cipherText.CopyTo(macInput, aes.IV.Length);

        using var hmac = new HMACSHA256(macKey);
        byte[] mac = hmac.ComputeHash(macInput);

        return $"2.{Convert.ToBase64String(aes.IV)}|{Convert.ToBase64String(cipherText)}|{Convert.ToBase64String(mac)}";
    }
}
