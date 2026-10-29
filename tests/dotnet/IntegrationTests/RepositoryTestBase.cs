using System;
using System.Security.Cryptography;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// Starts every test from empty tables. Concrete test classes carry [Collection(DatabaseCollection.Name)] so they
    /// share one database and run one at a time.
    /// </summary>
    public abstract class RepositoryTestBase
    {
        protected RepositoryTestBase(DatabaseFixture database)
        {
            Database = database;
            Data = database.Data;
            Log = new FakeLogger();
            database.ClearAllData();
        }

        protected DatabaseFixture Database { get; private set; }

        protected TestData Data { get; private set; }

        protected FakeLogger Log { get; private set; }

        /// <summary>Fails with the full text of whatever was logged, so a failure shows its own cause.</summary>
        protected void AssertNothingLogged()
        {
            Xunit.Assert.True(Log.Errors.Count == 0, "Errors were logged: " + string.Join(Environment.NewLine, Log.Errors));
        }

        /// <summary>
        /// A password hash in the format the first versions of the application wrote (salt + 20 bytes of PBKDF2-SHA1,
        /// 10,000 iterations, Base64), built here independently of PasswordUtility.
        /// </summary>
        protected static string LegacyHash(string password)
        {
            byte[] salt = new byte[16];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(salt);

            byte[] hash;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, 10000))
                hash = kdf.GetBytes(20);

            byte[] combined = new byte[36];
            Buffer.BlockCopy(salt, 0, combined, 0, 16);
            Buffer.BlockCopy(hash, 0, combined, 16, 20);
            return Convert.ToBase64String(combined);
        }
    }
}
