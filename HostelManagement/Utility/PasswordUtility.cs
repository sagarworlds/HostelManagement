using System;
using System.Security.Cryptography;

namespace HostelManagement
{
    /// <summary>
    /// Hashes and verifies passwords with PBKDF2.
    /// <para>
    /// Current format ("v2"), self-describing so the cost can be raised later without a migration:
    /// <c>v2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;</c>, PBKDF2-HMAC-SHA256, 16-byte salt, 32-byte hash.
    /// </para>
    /// <para>
    /// Legacy format, written by earlier versions and still accepted: <c>base64(salt[16] + hash[20])</c>,
    /// PBKDF2-HMAC-SHA1, 10,000 iterations. <see cref="NeedsRehash"/> flags those so they are upgraded at sign-in.
    /// </para>
    /// </summary>
    public class PasswordUtility
    {
        /// <summary>Work factor for new hashes (OWASP guidance for PBKDF2-HMAC-SHA256).</summary>
        public const int CurrentIterations = 600000;

        private const string CurrentPrefix = "v2$";
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        private const int LegacyIterations = 10000;
        private const int LegacyHashBytes = 20;

        // A stored iteration count outside this range cannot have been written by this class: treat the value as damaged
        // rather than spend minutes of CPU (or accept a trivially weak hash) because a row was tampered with.
        private const int MinimumStoredIterations = 1000;
        private const int MaximumStoredIterations = 10000000;

        /// <summary>Hashes a password with a fresh random salt at the current work factor.</summary>
        public static string HashPassword(string password)
        {
            return HashPassword(password, CurrentIterations);
        }

        /// <summary>Hashes at an explicit work factor; used to produce older-cost hashes and by tests.</summary>
        public static string HashPassword(string password, int iterations)
        {
            var salt = new byte[SaltBytes];
            using (var random = new RNGCryptoServiceProvider())
            {
                random.GetBytes(salt);
            }

            byte[] hash = DeriveV2(password, salt, iterations);
            return CurrentPrefix + iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>Checks a password against a stored hash in either format.</summary>
        /// <exception cref="FormatException">The stored value is not a hash this class could have written (damaged data).</exception>
        public static bool VerifyPassword(string enteredPassword, string hashedPassword)
        {
            if (hashedPassword != null && hashedPassword.StartsWith(CurrentPrefix, StringComparison.Ordinal))
            {
                int iterations;
                byte[] salt;
                byte[] expected;
                ParseV2(hashedPassword, out iterations, out salt, out expected);

                return SecureToken.FixedTimeEquals(DeriveV2(enteredPassword, salt, iterations), expected);
            }

            return VerifyLegacy(enteredPassword, hashedPassword);
        }

        /// <summary>
        /// True when a hash that verified correctly should be replaced with a fresh one: it is in the legacy format or
        /// was made with fewer iterations than today's. Call after a successful <see cref="VerifyPassword"/>.
        /// </summary>
        public static bool NeedsRehash(string hashedPassword)
        {
            if (hashedPassword == null || !hashedPassword.StartsWith(CurrentPrefix, StringComparison.Ordinal))
                return true;

            int iterations;
            byte[] salt;
            byte[] hash;
            ParseV2(hashedPassword, out iterations, out salt, out hash);
            return iterations < CurrentIterations;
        }

        private static byte[] DeriveV2(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashBytes);
            }
        }

        private static void ParseV2(string stored, out int iterations, out byte[] salt, out byte[] hash)
        {
            string[] parts = stored.Split('$');
            if (parts.Length != 4
                || !int.TryParse(parts[1], out iterations)
                || iterations < MinimumStoredIterations
                || iterations > MaximumStoredIterations)
            {
                throw new FormatException("The stored password hash is not in the expected v2 format.");
            }

            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltBytes || hash.Length != HashBytes)
                throw new FormatException("The stored password hash has an unexpected salt or hash length.");
        }

        private static bool VerifyLegacy(string enteredPassword, string hashedPassword)
        {
            byte[] stored = Convert.FromBase64String(hashedPassword ?? string.Empty);
            if (stored.Length != SaltBytes + LegacyHashBytes)
                throw new FormatException("The stored password hash has an unexpected length.");

            var salt = new byte[SaltBytes];
            Array.Copy(stored, 0, salt, 0, SaltBytes);
            var expected = new byte[LegacyHashBytes];
            Array.Copy(stored, SaltBytes, expected, 0, LegacyHashBytes);

            // The two-argument iteration constructor is PBKDF2-HMAC-SHA1, which is what the legacy format used.
            byte[] actual;
            using (var pbkdf2 = new Rfc2898DeriveBytes(enteredPassword, salt, LegacyIterations))
            {
                actual = pbkdf2.GetBytes(LegacyHashBytes);
            }

            return SecureToken.FixedTimeEquals(actual, expected);
        }
    }
}
