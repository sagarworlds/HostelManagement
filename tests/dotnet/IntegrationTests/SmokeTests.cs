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
    }
}
