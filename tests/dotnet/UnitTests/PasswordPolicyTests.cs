using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class PasswordPolicyTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("        ")]
        [InlineData("short")]
        [InlineData("1234567")]
        public void Rejects_passwords_that_are_empty_blank_or_too_short(string password)
        {
            Assert.NotNull(PasswordPolicy.Validate(password));
        }

        [Theory]
        [InlineData("r7Tq9xLm")]                 // exactly the minimum length
        [InlineData("a longer pass phrase")]
        public void Accepts_passwords_of_at_least_the_minimum_length(string password)
        {
            Assert.Null(PasswordPolicy.Validate(password));
        }

        [Theory]
        [InlineData("password")]
        [InlineData("PASSWORD")]        // the blocklist ignores case
        [InlineData("Password123")]
        [InlineData("12345678")]
        [InlineData("qwertyuiop")]
        [InlineData("HostelHub")]
        public void Rejects_passwords_attackers_try_first(string password)
        {
            string message = PasswordPolicy.Validate(password);

            Assert.NotNull(message);
            Assert.Contains("too easy to guess", message);
        }

        [Theory]
        [InlineData("aaaaaaaa")]
        [InlineData("00000000000")]
        public void Rejects_one_character_repeated(string password)
        {
            Assert.Contains("too easy to guess", PasswordPolicy.Validate(password));
        }

        [Fact]
        public void The_message_states_the_required_length()
        {
            Assert.Contains(PasswordPolicy.MinimumLength.ToString(), PasswordPolicy.Validate("x"));
        }
    }
}
