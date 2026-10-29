using System.Collections.Generic;
using System.Linq;
using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class TemporaryPasswordTests
    {
        // Same alphabet as the implementation: no I, O, 0 or 1.
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        [Fact]
        public void Generates_twelve_characters_from_the_unambiguous_alphabet()
        {
            string password = TemporaryPassword.Generate();

            Assert.Equal(12, password.Length);
            Assert.All(password, character => Assert.Contains(character, Alphabet));
        }

        [Fact]
        public void Generated_passwords_satisfy_the_password_policy()
        {
            Assert.Null(PasswordPolicy.Validate(TemporaryPassword.Generate()));
        }

        [Fact]
        public void Generated_passwords_do_not_repeat()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 2000; i++)
                Assert.True(seen.Add(TemporaryPassword.Generate()), "a password was generated twice");
        }

        [Fact]
        public void Every_symbol_is_equally_likely()
        {
            // 20,000 passwords x 12 characters = 240,000 draws over 32 symbols: 7,500 each, standard deviation about 85.
            // A +/-10% window is more than 8 standard deviations wide, so this fails for a biased generator
            // (for example a modulo over a length that does not divide 256) but not by chance.
            var counts = Alphabet.ToDictionary(symbol => symbol, symbol => 0);
            for (int i = 0; i < 20000; i++)
                foreach (char character in TemporaryPassword.Generate())
                    counts[character]++;

            foreach (var entry in counts)
                Assert.InRange(entry.Value, 6750, 8250);
        }
    }
}
