using System.Collections.Generic;
using System.Linq;
using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class SecureTokenTests
    {
        [Fact]
        public void Create_returns_43_url_safe_characters()
        {
            string token = SecureToken.Create();

            Assert.Equal(43, token.Length);   // 32 bytes as base64url without padding
            Assert.All(token, c => Assert.True(char.IsLetterOrDigit(c) || c == '-' || c == '_', "unexpected character " + c));
        }

        [Fact]
        public void Tokens_do_not_repeat()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 2000; i++)
                Assert.True(seen.Add(SecureToken.Create()));
        }

        [Theory]
        [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]   // NIST SHA-256 test vector
        [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
        [InlineData(null, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
        public void Sha256Hex_matches_the_standard_test_vectors(string input, string expected)
        {
            Assert.Equal(expected, SecureToken.Sha256Hex(input));
        }

        [Fact]
        public void FixedTimeEquals_compares_bytes()
        {
            Assert.True(SecureToken.FixedTimeEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
            Assert.False(SecureToken.FixedTimeEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }));
            Assert.False(SecureToken.FixedTimeEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }));
            Assert.False(SecureToken.FixedTimeEquals((byte[])null, new byte[] { 1 }));
            Assert.True(SecureToken.FixedTimeEquals(new byte[0], new byte[0]));
        }

        [Fact]
        public void FixedTimeEquals_compares_strings_and_treats_null_as_unequal()
        {
            Assert.True(SecureToken.FixedTimeEquals("abc", "abc"));
            Assert.False(SecureToken.FixedTimeEquals("abc", "abd"));
            Assert.False(SecureToken.FixedTimeEquals("abc", "ab"));
            Assert.False(SecureToken.FixedTimeEquals((string)null, "abc"));
            Assert.False(SecureToken.FixedTimeEquals((string)null, (string)null));
        }

        [Fact]
        public void A_difference_in_any_position_is_detected()
        {
            byte[] baseline = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            for (int position = 0; position < baseline.Length; position++)
            {
                byte[] changed = (byte[])baseline.Clone();
                changed[position] ^= 0x01;
                Assert.False(SecureToken.FixedTimeEquals(baseline, changed), "difference at " + position + " was missed");
            }
        }
    }
}
