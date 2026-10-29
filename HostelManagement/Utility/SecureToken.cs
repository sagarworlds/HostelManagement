using System;
using System.Security.Cryptography;
using System.Text;

namespace HostelManagement
{
    /// <summary>Random tokens, their hashes, and comparisons that do not leak how much of a guess was right.</summary>
    public static class SecureToken
    {
        private const int TokenBytes = 32;

        /// <summary>A URL-safe random token: 32 random bytes (256 bits) as base64url, 43 characters.</summary>
        public static string Create()
        {
            var bytes = new byte[TokenBytes];
            using (var generator = new RNGCryptoServiceProvider())
            {
                generator.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Lower-case hex SHA-256 of a token. Tokens that grant access (password reset links) are stored only in this
        /// form: they are random, so a plain hash is enough, and a leaked database cannot be replayed as links.
        /// </summary>
        public static string Sha256Hex(string value)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    hex.Append(b.ToString("x2"));

                return hex.ToString();
            }
        }

        /// <summary>
        /// Compares without stopping at the first difference, so the time taken does not reveal how much of a guess
        /// was right. (CryptographicOperations.FixedTimeEquals does not exist on .NET Framework.)
        /// </summary>
        public static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];

            return difference == 0;
        }

        public static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null)
                return false;

            return FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
        }
    }
}
