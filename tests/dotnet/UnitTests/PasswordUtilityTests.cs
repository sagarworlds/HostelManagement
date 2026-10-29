using System;
using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class PasswordUtilityTests
    {
        [Fact]
        public void A_password_verifies_against_its_own_hash()
        {
            string hash = PasswordUtility.HashPassword("correct horse battery");

            Assert.True(PasswordUtility.VerifyPassword("correct horse battery", hash));
        }

        [Fact]
        public void A_different_password_does_not_verify()
        {
            string hash = PasswordUtility.HashPassword("correct horse battery");

            Assert.False(PasswordUtility.VerifyPassword("Correct horse battery", hash));
            Assert.False(PasswordUtility.VerifyPassword(string.Empty, hash));
        }

        [Fact]
        public void The_same_password_hashes_differently_each_time_because_of_the_salt()
        {
            Assert.NotEqual(PasswordUtility.HashPassword("same"), PasswordUtility.HashPassword("same"));
        }

        [Fact]
        public void The_hash_is_base64_of_salt_plus_hash_36_bytes()
        {
            Assert.Equal(36, Convert.FromBase64String(PasswordUtility.HashPassword("anything")).Length);
        }

        [Fact]
        public void A_stored_value_that_is_not_base64_throws_FormatException()
        {
            // UserDB.IsPasswordValid relies on exactly this exception to detect a damaged hash and log it.
            Assert.Throws<FormatException>(() => PasswordUtility.VerifyPassword("x", "not base64!"));
        }
    }
}
