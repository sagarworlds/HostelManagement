using System;
using System.Collections.Generic;
using System.Linq;

namespace HostelManagement
{
    /// <summary>
    /// Rules a new password must meet: a minimum length, and not one of the passwords attackers try first. Length and a
    /// blocklist protect better than composition rules ("must contain a symbol"), which mostly produce "Password1!".
    /// Applied wherever a password is created or changed; it is deliberately not applied at sign-in, so accounts that
    /// predate a rule can still log in and then change their password.
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinimumLength = 8;

        // Eight characters or longer (shorter ones already fail the length rule); compared case-insensitively.
        private static readonly HashSet<string> CommonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "password1", "password12", "password123", "passw0rd", "p@ssw0rd", "p@ssword", "pass1234",
            "12345678", "123456789", "1234567890", "123123123", "987654321", "11111111", "00000000", "12341234",
            "qwertyui", "qwertyuiop", "qwerty123", "qwerty1234", "1q2w3e4r", "1qaz2wsx", "zaq12wsx", "asdfghjk",
            "iloveyou", "welcome1", "welcome123", "letmein1", "letmein123", "abc12345", "abcd1234", "admin123",
            "admin@123", "administrator", "changeme", "changeme123", "football", "baseball", "sunshine", "princess",
            "trustno1", "monkey123", "dragon123", "superman", "student123", "student1", "hostel123", "hostelhub"
        };

        /// <returns>null when the password is acceptable; otherwise a message that can be shown to the user.</returns>
        public static string Validate(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
                return "Choose a password of at least " + MinimumLength + " characters.";

            if (IsOneRepeatedCharacter(password) || CommonPasswords.Contains(password))
                return "That password is too easy to guess. Choose something less common.";

            return null;
        }

        private static bool IsOneRepeatedCharacter(string password)
        {
            return password.All(character => character == password[0]);
        }
    }
}
