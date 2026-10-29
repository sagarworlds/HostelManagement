namespace HostelManagement
{
    /// <summary>
    /// Rules a new password must meet. Applied wherever a password is created or changed; it is deliberately
    /// not applied at sign-in, so accounts that predate the rule can still log in and then change it.
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinimumLength = 8;

        /// <returns>null when the password is acceptable; otherwise a message that can be shown to the user.</returns>
        public static string Validate(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
                return "Choose a password of at least " + MinimumLength + " characters.";

            return null;
        }
    }
}
