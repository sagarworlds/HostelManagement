using System;

namespace HostelManagement
{
    /// <summary>How a request for a reset link was handled. None of these reveal whether the address has an account.</summary>
    public enum ResetRequestOutcome
    {
        /// <summary>Handled. Tell the user to check their e-mail if an account exists for the address.</summary>
        Accepted,

        /// <summary>Too many requests recently from this address or for this e-mail; nothing was sent.</summary>
        Throttled,

        /// <summary>E-mail is not set up on this installation, so the feature is unavailable.</summary>
        Disabled
    }

    /// <summary>
    /// Emailed password reset: sends a single-use link that expires after an hour, and later accepts a new password
    /// for it. Written against interfaces so the whole flow can be tested without a mail server or database.
    /// </summary>
    public class PasswordResetService
    {
        public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
        public static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);
        public const int MaximumRequestsPerEmail = 3;
        public const int MaximumRequestsPerAddress = 10;

        /// <summary>Shown when a link is unknown, already used or expired; one wording so the cases cannot be told apart.</summary>
        public const string InvalidLinkMessage = "This link is invalid or has expired. Request a new one.";

        private readonly IUserRepository _users;
        private readonly IPasswordResetRepository _resets;
        private readonly IEmailSender _email;
        private readonly ILogger _logger;
        private readonly Func<DateTime> _now;
        private readonly string _baseUrl;
        private readonly RateLimiter _perEmail;
        private readonly RateLimiter _perAddress;

        /// <param name="email">Null when e-mail is not configured, which disables the feature.</param>
        /// <param name="baseUrl">Absolute address of the site root; never derived from the request (a forged Host header would poison links).</param>
        /// <param name="perEmail">Limits requests for one e-mail address.</param>
        /// <param name="perAddress">Limits requests from one network address.</param>
        public PasswordResetService(IUserRepository users, IPasswordResetRepository resets, IEmailSender email,
            ILogger logger, Func<DateTime> now, string baseUrl, RateLimiter perEmail, RateLimiter perAddress)
        {
            if (users == null) throw new ArgumentNullException("users");
            if (resets == null) throw new ArgumentNullException("resets");
            if (logger == null) throw new ArgumentNullException("logger");
            if (now == null) throw new ArgumentNullException("now");
            if (perEmail == null) throw new ArgumentNullException("perEmail");
            if (perAddress == null) throw new ArgumentNullException("perAddress");

            _users = users;
            _resets = resets;
            _email = email;
            _logger = logger;
            _now = now;
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _perEmail = perEmail;
            _perAddress = perAddress;
        }

        public bool IsEnabled
        {
            get { return _email != null && SmtpSettings.IsValidBaseUrl(_baseUrl); }
        }

        /// <summary>
        /// E-mails a reset link when <paramref name="emailAddress"/> belongs to an account. The caller sees the same
        /// outcome whether or not it does, so the page cannot be used to find out who is registered.
        /// </summary>
        /// <exception cref="DataAccessException">The database failed while looking the account up.</exception>
        public ResetRequestOutcome RequestReset(string emailAddress, string requesterAddress)
        {
            if (!IsEnabled)
                return ResetRequestOutcome.Disabled;

            string mail = (emailAddress ?? string.Empty).Trim().ToLowerInvariant();
            string source = requesterAddress ?? string.Empty;

            if (_perEmail.RetryAfter(mail) > TimeSpan.Zero || _perAddress.RetryAfter(source) > TimeSpan.Zero)
            {
                _logger.Warn("Password reset requests throttled for an address (" + source + ").");
                return ResetRequestOutcome.Throttled;
            }

            // Every request counts, whether or not the account exists, so the limits cannot be probed either.
            _perEmail.Record(mail);
            _perAddress.Record(source);

            User user = _users.GetUserByEmail(mail);
            if (user == null)
            {
                _logger.Info("A password reset was requested for an address with no account.");
                return ResetRequestOutcome.Accepted;
            }

            DateTime now = _now();
            string token = SecureToken.Create();
            OperationResult stored = _resets.CreateToken(user.Id, SecureToken.Sha256Hex(token), now, now + TokenLifetime);
            if (!stored.Success)
                return ResetRequestOutcome.Accepted;   // the repository has logged why; the caller still must not learn the account exists

            try
            {
                _email.Send(user.Email, "Reset your HostelHub password", BuildBody(user, BuildLink(token)));
                _logger.Info("A password reset link was sent to user " + user.Id + ".");
            }
            catch (EmailDeliveryException ex)
            {
                _logger.Error("Could not send the password reset e-mail to user " + user.Id + ".", ex);
            }

            return ResetRequestOutcome.Accepted;
        }

        /// <summary>True when the link's token is known, unused and not expired.</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        public bool IsTokenValid(string token)
        {
            return !string.IsNullOrEmpty(token) && _resets.IsTokenValid(SecureToken.Sha256Hex(token), _now());
        }

        /// <summary>Sets the new password for the account the link belongs to and spends the link.</summary>
        public OperationResult Complete(string token, string newPassword)
        {
            if (string.IsNullOrEmpty(token))
                return OperationResult.Fail(InvalidLinkMessage);

            string policyProblem = PasswordPolicy.Validate(newPassword);
            if (policyProblem != null)
                return OperationResult.Fail(policyProblem);

            OperationResult result = _resets.CompleteReset(SecureToken.Sha256Hex(token), PasswordUtility.HashPassword(newPassword), _now());
            if (result.Success)
                _logger.Info("A password was reset through an e-mailed link.");

            return result;
        }

        /// <summary>The address placed in the e-mail. The token is escaped even though it is URL-safe by construction.</summary>
        public string BuildLink(string token)
        {
            return _baseUrl + "/ResetPassword.aspx?token=" + Uri.EscapeDataString(token);
        }

        private static string BuildBody(User user, string link)
        {
            return "Hello " + user.FirstName + "," + Environment.NewLine + Environment.NewLine +
                "Someone asked to reset the password of your HostelHub account. If that was you, open this link " +
                "within one hour to choose a new password. It works once:" + Environment.NewLine + Environment.NewLine +
                link + Environment.NewLine + Environment.NewLine +
                "If you did not ask for this, you can ignore this e-mail; your password has not changed.";
        }
    }
}
