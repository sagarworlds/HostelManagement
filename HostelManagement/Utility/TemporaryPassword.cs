using System.Security.Cryptography;

namespace HostelManagement
{
    /// <summary>Generates the one-time password an administrator hands to a student after a reset.</summary>
    public static class TemporaryPassword
    {
        // 32 symbols without the look-alikes I, O, 0 and 1: easy to read out or copy by hand.
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int Length = 12;

        /// <summary>A cryptographically random password of 12 characters (60 bits).</summary>
        public static string Generate()
        {
            var randomBytes = new byte[Length];
            using (var generator = new RNGCryptoServiceProvider())
            {
                generator.GetBytes(randomBytes);
            }

            var characters = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                // The alphabet has exactly 32 symbols, which divides 256 evenly, so masking the low five bits
                // picks every symbol with equal probability (a modulo of any other length would favour some).
                characters[i] = Alphabet[randomBytes[i] & 31];
            }

            return new string(characters);
        }
    }
}
