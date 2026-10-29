using System;

namespace HostelManagement
{
    /// <summary>
    /// Decides whether a sign-in attempt may proceed, combining three limits so one tactic cannot get round another:
    /// a strict limit for one e-mail from one address (guessing a password), a wider one per address (trying many
    /// accounts) and a wider one per e-mail (spreading guesses over many addresses without letting a stranger lock
    /// a victim out after a handful of tries).
    /// </summary>
    public class LoginAttemptPolicy
    {
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
        public const int MaximumFailuresPerEmailAndAddress = 5;
        public const int MaximumFailuresPerAddress = 30;
        public const int MaximumFailuresPerEmail = 20;

        private readonly LoginThrottle _perEmailAndAddress;
        private readonly LoginThrottle _perAddress;
        private readonly LoginThrottle _perEmail;

        public LoginAttemptPolicy(Func<DateTime> now)
        {
            _perEmailAndAddress = new LoginThrottle(MaximumFailuresPerEmailAndAddress, Window, now);
            _perAddress = new LoginThrottle(MaximumFailuresPerAddress, Window, now);
            _perEmail = new LoginThrottle(MaximumFailuresPerEmail, Window, now);
        }

        /// <returns>Zero when the attempt may proceed; otherwise how long to wait.</returns>
        public TimeSpan RetryAfter(string email, string address)
        {
            string mail = Normalize(email);
            string ip = address ?? string.Empty;

            TimeSpan wait = _perEmailAndAddress.RetryAfter(mail + "|" + ip);
            TimeSpan byAddress = _perAddress.RetryAfter(ip);
            TimeSpan byEmail = _perEmail.RetryAfter(mail);

            if (byAddress > wait) wait = byAddress;
            if (byEmail > wait) wait = byEmail;
            return wait;
        }

        /// <summary>Counts a failed attempt, including ones for unknown e-mails, so the response never reveals which exist.</summary>
        public void RecordFailure(string email, string address)
        {
            string mail = Normalize(email);
            string ip = address ?? string.Empty;

            _perEmailAndAddress.RecordFailure(mail + "|" + ip);
            _perAddress.RecordFailure(ip);
            _perEmail.RecordFailure(mail);
        }

        /// <summary>
        /// Clears only the pair that just succeeded. The wider counters are left alone, otherwise an attacker who owns
        /// one account could sign in to it between guesses to reset the address limit.
        /// </summary>
        public void RecordSuccess(string email, string address)
        {
            _perEmailAndAddress.Reset(Normalize(email) + "|" + (address ?? string.Empty));
        }

        private static string Normalize(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
