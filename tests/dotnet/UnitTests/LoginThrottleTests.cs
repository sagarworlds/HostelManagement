using System;
using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class FakeClock
    {
        public DateTime Now = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan by)
        {
            Now = Now + by;
        }
    }

    public class LoginThrottleTests
    {
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        [Fact]
        public void A_key_may_fail_up_to_the_allowance_and_is_then_blocked()
        {
            var clock = new FakeClock();
            var throttle = new LoginThrottle(3, Window, () => clock.Now);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(TimeSpan.Zero, throttle.RetryAfter("alice"));
                throttle.RecordFailure("alice");
            }

            Assert.True(throttle.RetryAfter("alice") > TimeSpan.Zero);
        }

        [Fact]
        public void The_wait_runs_until_the_oldest_counted_failure_ages_out()
        {
            var clock = new FakeClock();
            var throttle = new LoginThrottle(3, Window, () => clock.Now);

            throttle.RecordFailure("alice");           // t+0
            clock.Advance(TimeSpan.FromMinutes(1));
            throttle.RecordFailure("alice");           // t+1
            clock.Advance(TimeSpan.FromMinutes(1));
            throttle.RecordFailure("alice");           // t+2: blocked until the t+0 failure is 10 minutes old

            Assert.Equal(TimeSpan.FromMinutes(8), throttle.RetryAfter("alice"));

            clock.Advance(TimeSpan.FromMinutes(8));    // t+10
            Assert.Equal(TimeSpan.Zero, throttle.RetryAfter("alice"));
        }

        [Fact]
        public void Failures_outside_the_window_do_not_count()
        {
            var clock = new FakeClock();
            var throttle = new LoginThrottle(2, Window, () => clock.Now);

            throttle.RecordFailure("alice");
            throttle.RecordFailure("alice");
            Assert.True(throttle.RetryAfter("alice") > TimeSpan.Zero);

            clock.Advance(Window + TimeSpan.FromSeconds(1));
            Assert.Equal(TimeSpan.Zero, throttle.RetryAfter("alice"));

            throttle.RecordFailure("alice");           // one fresh failure is within the allowance again
            Assert.Equal(TimeSpan.Zero, throttle.RetryAfter("alice"));
        }

        [Fact]
        public void Keys_are_independent()
        {
            var clock = new FakeClock();
            var throttle = new LoginThrottle(1, Window, () => clock.Now);

            throttle.RecordFailure("alice");

            Assert.True(throttle.RetryAfter("alice") > TimeSpan.Zero);
            Assert.Equal(TimeSpan.Zero, throttle.RetryAfter("bob"));
        }

        [Fact]
        public void Reset_forgets_a_key()
        {
            var clock = new FakeClock();
            var throttle = new LoginThrottle(1, Window, () => clock.Now);
            throttle.RecordFailure("alice");

            throttle.Reset("alice");

            Assert.Equal(TimeSpan.Zero, throttle.RetryAfter("alice"));
        }

        [Fact]
        public void Flooding_with_made_up_keys_does_not_break_anything()
        {
            var clock = new FakeClock();
            var throttle = new LoginThrottle(3, Window, () => clock.Now);

            for (int i = 0; i < 30000; i++)
                throttle.RecordFailure("key-" + i);

            // Tracking stops at its cap, but a key that is already tracked is still limited, and nothing threw.
            throttle.RecordFailure("key-0");
            throttle.RecordFailure("key-0");
            Assert.True(throttle.RetryAfter("key-0") > TimeSpan.Zero);
        }

        [Fact]
        public void Invalid_settings_are_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LoginThrottle(0, Window, () => DateTime.UtcNow));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LoginThrottle(1, TimeSpan.Zero, () => DateTime.UtcNow));
            Assert.Throws<ArgumentNullException>(() => new LoginThrottle(1, Window, null));
        }
    }

    public class LoginAttemptPolicyTests
    {
        [Fact]
        public void Five_failures_for_one_email_from_one_address_block_that_pair()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);

            for (int i = 0; i < LoginAttemptPolicy.MaximumFailuresPerEmailAndAddress; i++)
            {
                Assert.Equal(TimeSpan.Zero, policy.RetryAfter("a@x.com", "10.0.0.1"));
                policy.RecordFailure("a@x.com", "10.0.0.1");
            }

            Assert.True(policy.RetryAfter("a@x.com", "10.0.0.1") > TimeSpan.Zero);
        }

        [Fact]
        public void The_block_does_not_follow_the_account_to_a_different_address()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);
            for (int i = 0; i < 5; i++)
                policy.RecordFailure("a@x.com", "10.0.0.1");

            // A stranger hammering an account must not lock the real owner out on a handful of tries.
            Assert.Equal(TimeSpan.Zero, policy.RetryAfter("a@x.com", "10.0.0.2"));
        }

        [Fact]
        public void E_mails_are_compared_ignoring_case_and_spaces()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);
            for (int i = 0; i < 5; i++)
                policy.RecordFailure("  A@X.com ", "10.0.0.1");

            Assert.True(policy.RetryAfter("a@x.com", "10.0.0.1") > TimeSpan.Zero);
        }

        [Fact]
        public void Trying_many_accounts_from_one_address_is_stopped_by_the_address_limit()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);

            for (int i = 0; i < LoginAttemptPolicy.MaximumFailuresPerAddress; i++)
                policy.RecordFailure("user" + i + "@x.com", "10.0.0.9");

            Assert.True(policy.RetryAfter("someone-new@x.com", "10.0.0.9") > TimeSpan.Zero);
        }

        [Fact]
        public void Spreading_guesses_over_many_addresses_is_stopped_by_the_e_mail_limit()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);

            for (int i = 0; i < LoginAttemptPolicy.MaximumFailuresPerEmail; i++)
                policy.RecordFailure("victim@x.com", "10.0.1." + i);

            Assert.True(policy.RetryAfter("victim@x.com", "10.9.9.9") > TimeSpan.Zero);
        }

        [Fact]
        public void A_successful_sign_in_clears_only_its_own_pair()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);

            // An attacker with one real account guesses elsewhere and signs in to their own account between guesses.
            for (int i = 0; i < LoginAttemptPolicy.MaximumFailuresPerAddress; i++)
                policy.RecordFailure("user" + i + "@x.com", "10.0.0.9");
            policy.RecordSuccess("mine@x.com", "10.0.0.9");

            Assert.True(policy.RetryAfter("mine@x.com", "10.0.0.9") > TimeSpan.Zero);
        }

        [Fact]
        public void A_success_after_a_few_failures_resets_the_pair()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);
            for (int i = 0; i < 4; i++)
                policy.RecordFailure("a@x.com", "10.0.0.1");

            policy.RecordSuccess("a@x.com", "10.0.0.1");
            policy.RecordFailure("a@x.com", "10.0.0.1");

            Assert.Equal(TimeSpan.Zero, policy.RetryAfter("a@x.com", "10.0.0.1"));
        }

        [Fact]
        public void Everything_is_allowed_again_once_the_window_has_passed()
        {
            var clock = new FakeClock();
            var policy = new LoginAttemptPolicy(() => clock.Now);
            for (int i = 0; i < 5; i++)
                policy.RecordFailure("a@x.com", "10.0.0.1");

            clock.Advance(LoginAttemptPolicy.Window + TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.Zero, policy.RetryAfter("a@x.com", "10.0.0.1"));
        }
    }
}
