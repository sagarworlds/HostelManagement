using Xunit;

namespace HostelManagement.IntegrationTests
{
    [Collection(DatabaseCollection.Name)]
    public class SmokeTests
    {
        private readonly DatabaseFixture _database;

        public SmokeTests(DatabaseFixture database)
        {
            _database = database;
            _database.ClearAllData();
        }

        [Fact]
        public void The_applications_own_DBAccess_connects_to_the_test_database()
        {
            using (var db = new DBAccess())
            {
                Assert.True(db.isConnectionOpen());
                Assert.Equal(DatabaseFixture.TestDatabase, db.lfnExecuteScaler<string>("SELECT DB_NAME();"));
            }
        }

        [Fact]
        public void The_repositories_run_as_the_least_privileged_login_not_as_an_administrator()
        {
            // If this ever fails, the rest of the suite would be proving nothing about the deployment recommendation.
            using (var db = new DBAccess())
            {
                Assert.Equal(DatabaseFixture.ApplicationLogin, db.lfnExecuteScaler<string>("SELECT SUSER_SNAME();"));
                Assert.Equal(0, db.lfnExecuteScaler<int>("SELECT CAST(IS_MEMBER('db_owner') AS INT);"));
                Assert.Equal(0, db.lfnExecuteScaler<int>("SELECT CAST(IS_SRVROLEMEMBER('sysadmin') AS INT);"));
                Assert.Equal(1, db.lfnExecuteScaler<int>("SELECT CAST(IS_MEMBER('db_datareader') AS INT);"));
                Assert.Equal(1, db.lfnExecuteScaler<int>("SELECT CAST(IS_MEMBER('db_datawriter') AS INT);"));
            }
        }
    }
}
