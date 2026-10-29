using System;
using System.Data.SqlClient;
using System.IO;
using System.Security;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// Builds a disposable database by running Database/schema.sql on the server named by the environment variable
    /// <see cref="ServerVariable"/>, and points the application's own <see cref="Config"/> at it, so the repositories
    /// run exactly as they do in the web application.
    /// </summary>
    /// <remarks>
    /// The only database this class ever creates or drops is <see cref="TestDatabase"/>; a real HMSDB on the same
    /// server is never touched. Use a throw-away server anyway (tests/start-sql-server.sh starts one with Docker).
    /// </remarks>
    public sealed class DatabaseFixture : IDisposable
    {
        public const string ServerVariable = "HOSTELHUB_TEST_SQLSERVER";
        public const string TestDatabase = "HMSDB_IntegrationTest";

        private readonly string _configFile;

        public DatabaseFixture()
        {
            string value = Environment.GetEnvironmentVariable(ServerVariable);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    "Set " + ServerVariable + " to a connection string for a SQL Server these tests may create a database on, e.g. " +
                    "\"Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True\". " +
                    "tests/start-sql-server.sh starts a disposable one with Docker.");

            var builder = new SqlConnectionStringBuilder(value) { InitialCatalog = "master", Pooling = true };
            ServerConnectionString = builder.ConnectionString;
            builder.InitialCatalog = TestDatabase;
            DatabaseConnectionString = builder.ConnectionString;

            Rebuild(ReadDatabaseScript("schema.sql"));

            _configFile = WriteApplicationConfig(DatabaseConnectionString);
            // Must happen before anything reads ConfigurationManager (Config.MCQBConnectionString does, on first use).
            AppContext.SetData("APP_CONFIG_FILE", _configFile);

            Data = new TestData(DatabaseConnectionString);
        }

        /// <summary>Connection string for the server's master database, used to create and drop the test database.</summary>
        public string ServerConnectionString { get; private set; }

        /// <summary>Connection string for the test database; also what the application's Config reads.</summary>
        public string DatabaseConnectionString { get; private set; }

        /// <summary>Direct SQL for arranging and checking data, independent of the repositories under test.</summary>
        public TestData Data { get; private set; }

        /// <summary>Contents of a file from the Database folder, as copied next to the test assembly.</summary>
        public static string ReadDatabaseScript(string fileName)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Database", fileName));
        }

        /// <summary>
        /// Throws the test database away and runs <paramref name="script"/> (which must create it, like schema.sql does)
        /// to make a new one. Tests that start from some other database, such as one made by an earlier version of the
        /// application, call this and must call <see cref="Rebuild()"/> afterwards so later tests find the normal schema.
        /// </summary>
        public void Rebuild(string script)
        {
            DropTestDatabase();
            SqlScript.Run(ServerConnectionString, SqlScript.Retarget(script, TestDatabase));
        }

        /// <summary>Back to a fresh database built by schema.sql.</summary>
        public void Rebuild()
        {
            Rebuild(ReadDatabaseScript("schema.sql"));
        }

        /// <summary>Empties every table so each test starts from the same state. Children first, because of the foreign keys.</summary>
        public void ClearAllData()
        {
            Data.Execute(
                "DELETE FROM dbo.tbl_PasswordReset; DELETE FROM dbo.tbl_RequestRoom; DELETE FROM dbo.tbl_Announcement; " +
                "DELETE FROM dbo.tbl_Room; DELETE FROM dbo.tbl_User;");
        }

        public void Dispose()
        {
            DropTestDatabase();
            if (_configFile != null && File.Exists(_configFile))
                File.Delete(_configFile);
        }

        private void DropTestDatabase()
        {
            // Pooled connections would keep the database busy; the single-user switch also kicks out any stragglers.
            SqlConnection.ClearAllPools();
            SqlScript.Run(ServerConnectionString,
                "IF DB_ID(N'" + TestDatabase + "') IS NOT NULL\r\nBEGIN\r\n" +
                "    ALTER DATABASE [" + TestDatabase + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;\r\n" +
                "    DROP DATABASE [" + TestDatabase + "];\r\nEND");
        }

        private static string WriteApplicationConfig(string connectionString)
        {
            string path = Path.Combine(Path.GetTempPath(), "hostelhub-integration-" + Guid.NewGuid().ToString("N") + ".config");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><configuration><connectionStrings>" +
                "<add name=\"DBConnection\" connectionString=\"" + SecurityElement.Escape(connectionString) + "\" providerName=\"System.Data.SqlClient\" />" +
                "</connectionStrings></configuration>");
            return path;
        }
    }

    [CollectionDefinition(Name)]
    public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        /// <summary>All database tests share one database, so they run one after another.</summary>
        public const string Name = "Database";
    }
}
