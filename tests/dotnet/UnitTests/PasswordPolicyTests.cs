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
        [InlineData("12345678")]
        [InlineData("a longer pass phrase")]
        public void Accepts_passwords_of_at_least_the_minimum_length(string password)
        {
            Assert.Null(PasswordPolicy.Validate(password));
        }

        [Fact]
        public void The_message_states_the_required_length()
        {
            Assert.Contains(PasswordPolicy.MinimumLength.ToString(), PasswordPolicy.Validate("x"));
        }
    }
}
