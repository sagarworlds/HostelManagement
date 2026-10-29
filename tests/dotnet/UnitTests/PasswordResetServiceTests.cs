using System;
using System.Collections.Generic;
using System.Linq;
using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    internal class FakeLogger : ILogger
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public void Info(string message) { Infos.Add(message); }
        public void Warn(string message) { Warnings.Add(message); }
        public void Error(string message, Exception ex) { Errors.Add(message); }
    }

    internal class FakeEmailSender : IEmailSender
    {
        public class Message { public string To, Subject, Body; }

        public readonly List<Message> Sent = new List<Message>();
        public bool Fail;

        public void Send(string toAddress, string subject, string body)
        {
            if (Fail)
                throw new EmailDeliveryException("simulated outage", new Exception());

            Sent.Add(new Message { To = toAddress, Subject = subject, Body = body });
        }
    }

    internal class FakeResets : IPasswordResetRepository
    {
        public class Token { public int UserId; public string Hash; public DateTime CreatedOn, ExpiresOn; }

        public readonly List<Token> Created = new List<Token>();
        public bool CreateFails;
        public string ValidatedHash;
        public bool TokenIsValid = true;
        public string CompletedHash, CompletedPasswordHash;
        public DateTime CompletedAt;
        public OperationResult CompleteResult = OperationResult.Ok();

        public OperationResult CreateToken(int userId, string tokenHash, DateTime createdOn, DateTime expiresOn)
        {
            if (CreateFails) return OperationResult.Fail("db down");
            Created.Add(new Token { UserId = userId, Hash = tokenHash, CreatedOn = createdOn, ExpiresOn = expiresOn });
            return OperationResult.Ok();
        }

        public bool IsTokenValid(string tokenHash, DateTime now) { ValidatedHash = tokenHash; return TokenIsValid; }

        public OperationResult CompleteReset(string tokenHash, string newPasswordHash, DateTime now)
        {
            CompletedHash = tokenHash; CompletedPasswordHash = newPasswordHash; CompletedAt = now;
            return CompleteResult;
        }
    }

    internal class FakeUsers : IUserRepository
    {
        public readonly Dictionary<string, User> ByEmail = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        public int Lookups;

        public User GetUserByEmail(string email) { Lookups++; User user; return ByEmail.TryGetValue(email, out user) ? user : null; }

        public OperationResult AddUser(User user) { throw new NotSupportedException(); }
        public OperationResult UpdateUser(User user) { throw new NotSupportedException(); }
        public OperationResult UpdateProfile(User user) { throw new NotSupportedException(); }
        public OperationResult ChangePassword(int userId, string currentPassword, string newPassword) { throw new NotSupportedException(); }
        public OperationResult ResetStudentPassword(int studentId, string temporaryPassword) { throw new NotSupportedException(); }
        public User Authenticate(string email, string password) { throw new NotSupportedException(); }
        public bool EmailExists(string email, int excludeUserId) { throw new NotSupportedException(); }
        public List<User> GetUsersByType(string userType) { throw new NotSupportedException(); }
        public User GetUserById(int id) { throw new NotSupportedException(); }
        public OperationResult DeleteStudent(int id) { throw new NotSupportedException(); }
        public OperationResult DeleteAdmin(int id, int currentAdminId) { throw new NotSupportedException(); }
    }

    public class PasswordResetServiceTests
    {
        private const string BaseUrl = "https://hostel.example.com/";

        private readonly FakeClock _clock = new FakeClock();
        private readonly FakeUsers _users = new FakeUsers();
        private readonly FakeResets _resets = new FakeResets();
        private readonly FakeEmailSender _email = new FakeEmailSender();
        private readonly FakeLogger _logger = new FakeLogger();

        public PasswordResetServiceTests()
        {
            _users.ByEmail["asha@example.com"] = new User { Id = 7, Email = "asha@example.com", FirstName = "Asha", LastName = "Rao" };
        }

        private PasswordResetService Create(IEmailSender email = null, bool enabled = true)
        {
            return new PasswordResetService(_users, _resets, enabled ? (email ?? _email) : null, _logger, () => _clock.Now, BaseUrl,
                new RateLimiter(PasswordResetService.MaximumRequestsPerEmail, PasswordResetService.RequestWindow, () => _clock.Now),
                new RateLimiter(PasswordResetService.MaximumRequestsPerAddress, PasswordResetService.RequestWindow, () => _clock.Now));
        }

        [Fact]
        public void A_known_address_gets_one_e_mail_with_a_link_to_its_own_token()
        {
            var service = Create();

            ResetRequestOutcome outcome = service.RequestReset("asha@example.com", "10.0.0.1");

            Assert.Equal(ResetRequestOutcome.Accepted, outcome);
            var message = Assert.Single(_email.Sent);
            Assert.Equal("asha@example.com", message.To);
            Assert.Contains("Hello Asha,", message.Body);

            // The e-mail carries the raw token; the store holds only its hash.
            var stored = Assert.Single(_resets.Created);
            Assert.Equal(7, stored.UserId);
            string prefix = "https://hostel.example.com/ResetPassword.aspx?token=";
            string link = message.Body.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith(prefix));
            string token = Uri.UnescapeDataString(link.Substring(prefix.Length));
            Assert.Equal(SecureToken.Sha256Hex(token), stored.Hash);
            Assert.DoesNotContain(token, stored.Hash);
        }

        [Fact]
        public void The_link_expires_after_one_hour()
        {
            Create().RequestReset("asha@example.com", "10.0.0.1");

            var stored = Assert.Single(_resets.Created);
            Assert.Equal(_clock.Now, stored.CreatedOn);
            Assert.Equal(_clock.Now.AddHours(1), stored.ExpiresOn);
        }

        [Fact]
        public void An_unknown_address_gets_the_same_outcome_and_no_e_mail()
        {
            ResetRequestOutcome known = Create().RequestReset("asha@example.com", "10.0.0.1");
            _email.Sent.Clear();
            _resets.Created.Clear();

            ResetRequestOutcome unknown = Create().RequestReset("nobody@example.com", "10.0.0.2");

            Assert.Equal(known, unknown);     // the page cannot be used to learn who is registered
            Assert.Empty(_email.Sent);
            Assert.Empty(_resets.Created);
        }

        [Fact]
        public void The_address_is_matched_ignoring_case_and_spaces()
        {
            Create().RequestReset("  ASHA@Example.COM ", "10.0.0.1");

            Assert.Single(_email.Sent);
        }

        [Fact]
        public void A_fourth_request_for_the_same_e_mail_within_the_hour_is_throttled()
        {
            var service = Create();
            for (int i = 0; i < PasswordResetService.MaximumRequestsPerEmail; i++)
                Assert.Equal(ResetRequestOutcome.Accepted, service.RequestReset("asha@example.com", "10.0.0." + i));
            _email.Sent.Clear();

            Assert.Equal(ResetRequestOutcome.Throttled, service.RequestReset("asha@example.com", "10.0.9.9"));
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public void Requests_for_unknown_addresses_are_counted_too()
        {
            var service = Create();
            for (int i = 0; i < PasswordResetService.MaximumRequestsPerEmail; i++)
                service.RequestReset("nobody@example.com", "10.0.0." + i);

            // If only real accounts were limited, the limit itself would reveal which addresses are registered.
            Assert.Equal(ResetRequestOutcome.Throttled, service.RequestReset("nobody@example.com", "10.0.9.9"));
        }

        [Fact]
        public void One_address_asking_for_many_accounts_is_throttled()
        {
            var service = Create();
            for (int i = 0; i < PasswordResetService.MaximumRequestsPerAddress; i++)
                service.RequestReset("person" + i + "@example.com", "10.0.0.1");

            Assert.Equal(ResetRequestOutcome.Throttled, service.RequestReset("another@example.com", "10.0.0.1"));
        }

        [Fact]
        public void Requests_are_allowed_again_after_the_window()
        {
            var service = Create();
            for (int i = 0; i < PasswordResetService.MaximumRequestsPerEmail; i++)
                service.RequestReset("asha@example.com", "10.0.0." + i);

            _clock.Advance(PasswordResetService.RequestWindow + TimeSpan.FromSeconds(1));

            Assert.Equal(ResetRequestOutcome.Accepted, service.RequestReset("asha@example.com", "10.0.9.9"));
        }

        [Fact]
        public void Without_mail_settings_the_feature_is_disabled_and_nothing_is_touched()
        {
            var service = Create(enabled: false);

            Assert.False(service.IsEnabled);
            Assert.Equal(ResetRequestOutcome.Disabled, service.RequestReset("asha@example.com", "10.0.0.1"));
            Assert.Equal(0, _users.Lookups);
            Assert.Empty(_resets.Created);
        }

        [Fact]
        public void A_mail_failure_is_logged_and_the_caller_still_sees_the_normal_outcome()
        {
            _email.Fail = true;

            ResetRequestOutcome outcome = Create().RequestReset("asha@example.com", "10.0.0.1");

            Assert.Equal(ResetRequestOutcome.Accepted, outcome);
            Assert.Contains(_logger.Errors, e => e.Contains("Could not send the password reset e-mail to user 7"));
        }

        [Fact]
        public void If_the_token_cannot_be_stored_no_e_mail_is_sent()
        {
            _resets.CreateFails = true;

            ResetRequestOutcome outcome = Create().RequestReset("asha@example.com", "10.0.0.1");

            Assert.Equal(ResetRequestOutcome.Accepted, outcome);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public void The_log_never_contains_the_token()
        {
            Create().RequestReset("asha@example.com", "10.0.0.1");

            var token = _email.Sent[0].Body;
            var everything = string.Join("\n", _logger.Infos.Concat(_logger.Warnings).Concat(_logger.Errors));
            Assert.DoesNotContain("ResetPassword.aspx", everything);
            Assert.DoesNotContain(_resets.Created[0].Hash, everything);
            Assert.NotEmpty(token);
        }

        [Fact]
        public void Links_use_the_configured_address_and_escape_the_token()
        {
            Assert.Equal("https://hostel.example.com/ResetPassword.aspx?token=a%2Bb%2Fc", Create().BuildLink("a+b/c"));
        }

        [Fact]
        public void Token_validity_is_checked_against_the_hash_not_the_raw_token()
        {
            Create().IsTokenValid("secret-token");

            Assert.Equal(SecureToken.Sha256Hex("secret-token"), _resets.ValidatedHash);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void A_missing_token_is_never_valid(string token)
        {
            Assert.False(Create().IsTokenValid(token));
            Assert.Null(_resets.ValidatedHash);
        }

        [Fact]
        public void Completing_hashes_the_new_password_and_spends_the_token_by_its_hash()
        {
            OperationResult result = Create().Complete("secret-token", "a good new passphrase");

            Assert.True(result.Success);
            Assert.Equal(SecureToken.Sha256Hex("secret-token"), _resets.CompletedHash);
            Assert.StartsWith("v2$", _resets.CompletedPasswordHash);
            Assert.True(PasswordUtility.VerifyPassword("a good new passphrase", _resets.CompletedPasswordHash));
            Assert.Equal(_clock.Now, _resets.CompletedAt);
        }

        [Fact]
        public void A_weak_new_password_is_refused_before_the_token_is_spent()
        {
            OperationResult result = Create().Complete("secret-token", "password");

            Assert.False(result.Success);
            Assert.Null(_resets.CompletedHash);
        }

        [Fact]
        public void A_missing_token_cannot_complete_a_reset()
        {
            OperationResult result = Create().Complete(null, "a good new passphrase");

            Assert.False(result.Success);
            Assert.Equal(PasswordResetService.InvalidLinkMessage, result.Message);
            Assert.Null(_resets.CompletedHash);
        }

        [Fact]
        public void A_repository_refusal_is_passed_back()
        {
            _resets.CompleteResult = OperationResult.Fail(PasswordResetService.InvalidLinkMessage);

            OperationResult result = Create().Complete("used-token", "a good new passphrase");

            Assert.False(result.Success);
            Assert.Equal(PasswordResetService.InvalidLinkMessage, result.Message);
        }
    }

    public class SmtpSettingsTests
    {
        private static SmtpSettings From(params string[] pairs)
        {
            var values = new Dictionary<string, string>();
            for (int i = 0; i < pairs.Length; i += 2) values[pairs[i]] = pairs[i + 1];
            return SmtpSettings.From(key => { string v; return values.TryGetValue(key, out v) ? v : null; });
        }

        [Fact]
        public void A_complete_set_of_settings_is_configured()
        {
            SmtpSettings s = From("Smtp.Host", "smtp.example.com", "Smtp.FromAddress", "no-reply@example.com", "App.BaseUrl", "https://hostel.example.com/");

            Assert.True(s.IsConfigured);
            Assert.Equal("https://hostel.example.com", s.BaseUrl);   // trailing slash trimmed
            Assert.Equal(587, s.Port);                                // defaults
            Assert.True(s.EnableSsl);
            Assert.Equal("HostelHub", s.FromName);
        }

        [Theory]
        [InlineData(null, "no-reply@example.com", "https://hostel.example.com")]          // no host
        [InlineData("smtp.example.com", null, "https://hostel.example.com")]              // no sender
        [InlineData("smtp.example.com", "not an address", "https://hostel.example.com")]  // bad sender
        [InlineData("smtp.example.com", "no-reply@example.com", null)]                     // no public address
        [InlineData("smtp.example.com", "no-reply@example.com", "hostel.example.com")]     // not absolute
        [InlineData("smtp.example.com", "no-reply@example.com", "http://hostel.example.com")] // plain http to another machine
        public void Anything_missing_or_unsafe_leaves_the_feature_off(string host, string from, string baseUrl)
        {
            Assert.False(From("Smtp.Host", host, "Smtp.FromAddress", from, "App.BaseUrl", baseUrl).IsConfigured);
        }

        [Fact]
        public void Plain_http_is_allowed_for_localhost_development()
        {
            Assert.True(From("Smtp.Host", "localhost", "Smtp.FromAddress", "dev@example.com", "App.BaseUrl", "http://localhost:5000").IsConfigured);
        }

        [Fact]
        public void Port_and_ssl_can_be_overridden_and_nonsense_falls_back()
        {
            SmtpSettings custom = From("Smtp.Port", "2525", "Smtp.EnableSsl", "false");
            Assert.Equal(2525, custom.Port);
            Assert.False(custom.EnableSsl);

            SmtpSettings nonsense = From("Smtp.Port", "99999", "Smtp.EnableSsl", "maybe");
            Assert.Equal(587, nonsense.Port);
            Assert.True(nonsense.EnableSsl);
        }
    }
}
