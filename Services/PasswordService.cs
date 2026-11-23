using System;
using System.Security.Cryptography;

namespace TurnoutStore.Services
{
    public interface IPasswordService
    {
        string HashPassword(string password);
        bool VerifyPassword(string enteredPassword, string storedHash);
    }

    public class PasswordService : IPasswordService
    {
        private const string VersionPrefix = "V1$";
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32;  // 256 bit
        private const int Iterations = 600000; // Exceeds OWASP recommendation for PBKDF2-SHA512
        private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithm,
                KeySize);

            // Format: V1$<salt_base64>$<key_base64>
            return $"{VersionPrefix}{Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public bool VerifyPassword(string enteredPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(enteredPassword) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            // Guest users must NEVER be authenticated via password
            if (storedHash.StartsWith("GUEST_CHECKOUT_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 1. Modern PBKDF2-SHA512 with V1$ prefix
            if (storedHash.StartsWith(VersionPrefix, StringComparison.Ordinal))
            {
                var parts = storedHash.Substring(VersionPrefix.Length).Split('$');
                if (parts.Length != 2)
                {
                    return false;
                }

                try
                {
                    byte[] salt = Convert.FromBase64String(parts[0]);
                    byte[] expectedKey = Convert.FromBase64String(parts[1]);

                    byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(
                        enteredPassword,
                        salt,
                        Iterations,
                        HashAlgorithm,
                        expectedKey.Length);

                    return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
                }
                catch
                {
                    return false;
                }
            }

            // 2. Legacy fallback PBKDF2-SHA256 (48-byte format from early development)
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);
                if (hashBytes.Length == 48)
                {
                    byte[] salt = new byte[16];
                    Array.Copy(hashBytes, 0, salt, 0, 16);

                    using var pbkdf2 = new Rfc2898DeriveBytes(enteredPassword, salt, 10000, HashAlgorithmName.SHA256);
                    byte[] actualKey = pbkdf2.GetBytes(32);

                    byte[] expectedKey = new byte[32];
                    Array.Copy(hashBytes, 16, expectedKey, 0, 32);

                    return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
                }
            }
            catch
            {
                // Not valid base64 legacy hash
            }

            return false;
        }
    }
}
