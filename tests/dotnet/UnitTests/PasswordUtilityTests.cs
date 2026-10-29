using System;
using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class PasswordUtilityTests
    {
        private const string VectorPassword = "correct horse battery";

        // Known answers computed independently with Python's hashlib.pbkdf2_hmac, not with the code under test:
        //   salt = bytes 0x01..0x10; v2 = PBKDF2-HMAC-SHA256, 1000 iterations, 32 bytes;
        //   legacy = base64(salt + PBKDF2-HMAC-SHA1, 10000 iterations, 20 bytes).
        private const string V2Vector = "v2$1000$AQIDBAUGBwgJCgsMDQ4PEA==$OZisqJloVWQ/xF8cg5vJoGqp86fPH5Ob3F8cZLPh8Zs=";
        private const string LegacyVector = "AQIDBAUGBwgJCgsMDQ4PEK+oCRA4WAbH2cMIF9ic+JN1lo/u";

        [Fact]
        public void A_new_hash_is_v2_at_the_current_cost_with_a_16_byte_salt_and_32_byte_hash()
        {
            string[] parts = PasswordUtility.HashPassword("anything at all").Split('$');

            Assert.Equal(4, parts.Length);
            Assert.Equal("v2", parts[0]);
            Assert.Equal(PasswordUtility.CurrentIterations.ToString(), parts[1]);
            Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
            Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
        }

        [Fact]
        public void A_password_verifies_against_its_own_hash()
        {
            string hash = PasswordUtility.HashPassword(VectorPassword);

            Assert.True(PasswordUtility.VerifyPassword(VectorPassword, hash));
        }

        [Fact]
        public void A_different_password_does_not_verify()
        {
            string hash = PasswordUtility.HashPassword(VectorPassword);

            Assert.False(PasswordUtility.VerifyPassword("Correct horse battery", hash));
            Assert.False(PasswordUtility.VerifyPassword(string.Empty, hash));
        }

        [Fact]
        public void The_same_password_hashes_differently_each_time_because_of_the_salt()
        {
            Assert.NotEqual(PasswordUtility.HashPassword("same", 1000), PasswordUtility.HashPassword("same", 1000));
        }

        [Fact]
        public void The_v2_algorithm_matches_an_independent_implementation()
        {
            Assert.True(PasswordUtility.VerifyPassword(VectorPassword, V2Vector));
            Assert.False(PasswordUtility.VerifyPassword("wrong", V2Vector));
        }

        [Fact]
        public void Hashes_written_by_earlier_versions_still_verify()
        {
            Assert.True(PasswordUtility.VerifyPassword(VectorPassword, LegacyVector));
            Assert.False(PasswordUtility.VerifyPassword("wrong", LegacyVector));
        }

        [Fact]
        public void Legacy_hashes_and_low_cost_hashes_are_flagged_for_upgrade()
        {
            Assert.True(PasswordUtility.NeedsRehash(LegacyVector));
            Assert.True(PasswordUtility.NeedsRehash(V2Vector));   // v2 but only 1000 iterations
            Assert.True(PasswordUtility.NeedsRehash(null));
        }

        [Fact]
        public void A_hash_at_the_current_cost_is_not_flagged()
        {
            Assert.False(PasswordUtility.NeedsRehash(PasswordUtility.HashPassword("anything at all")));
        }

        [Fact]
        public void An_upgraded_hash_verifies_with_the_same_password()
        {
            // What UserDB does after a successful legacy sign-in: hash the verified password again.
            Assert.True(PasswordUtility.VerifyPassword(VectorPassword, LegacyVector));
            string upgraded = PasswordUtility.HashPassword(VectorPassword);

            Assert.False(PasswordUtility.NeedsRehash(upgraded));
            Assert.True(PasswordUtility.VerifyPassword(VectorPassword, upgraded));
        }

        [Theory]
        [InlineData("not base64!")]                                            // legacy path: not base64
        [InlineData("AAAA")]                                                   // legacy path: wrong length
        [InlineData("")]                                                       // legacy path: empty
        [InlineData("v2$")]                                                    // too few parts
        [InlineData("v2$abc$AQIDBAUGBwgJCgsMDQ4PEA==$OZisqJloVWQ/xF8cg5vJoGqp86fPH5Ob3F8cZLPh8Zs=")]   // iterations not a number
        [InlineData("v2$10$AQIDBAUGBwgJCgsMDQ4PEA==$OZisqJloVWQ/xF8cg5vJoGqp86fPH5Ob3F8cZLPh8Zs=")]    // implausibly low cost
        [InlineData("v2$99999999$AQIDBAUGBwgJCgsMDQ4PEA==$OZisqJloVWQ/xF8cg5vJoGqp86fPH5Ob3F8cZLPh8Zs=")] // implausibly high cost
        [InlineData("v2$1000$AQID$OZisqJloVWQ/xF8cg5vJoGqp86fPH5Ob3F8cZLPh8Zs=")]                      // salt too short
        [InlineData("v2$1000$AQIDBAUGBwgJCgsMDQ4PEA==$AQID")]                                           // hash too short
        public void A_stored_value_that_is_not_a_valid_hash_throws_FormatException(string stored)
        {
            // UserDB.IsPasswordValid relies on exactly this exception to detect a damaged hash and log it.
            Assert.Throws<FormatException>(() => PasswordUtility.VerifyPassword("x", stored));
        }
    }
}
