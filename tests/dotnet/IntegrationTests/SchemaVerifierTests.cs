using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// The check that the database has what this version of the application needs, run on a real server: that it passes on
    /// the database schema.sql builds, that it names what is missing, and that the list it checks against cannot drift from
    /// the schema script.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class SchemaVerifierTests : RepositoryTestBase
    {
        public SchemaVerifierTests(DatabaseFixture database) : base(database)
        {
        }

        private SchemaCheckResult Verify()
        {
            return new SchemaVerifier(Log).Verify();
        }

        [Fact]
        public void A_database_made_by_schema_sql_has_everything_the_application_needs()
        {
            SchemaCheckResult result = Verify();

            Assert.True(result.IsCurrent, string.Join(" ", result.Problems));
            AssertNothingLogged();
        }

        [Fact]
        public void The_expected_schema_is_exactly_what_schema_sql_creates_so_neither_can_drift_from_the_other()
        {
            DataTable columns = SqlScript.Run(Database.DatabaseConnectionString,
                "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = N'dbo';")[0];
            var created = columns.Rows.Cast<DataRow>().Select(row => row["TABLE_NAME"] + "." + row["COLUMN_NAME"]).ToList();
            var expected = ExpectedSchema.TablesAndColumns.SelectMany(table => table.Value.Select(column => table.Key + "." + column)).ToList();

            Assert.Empty(expected.Except(created, StringComparer.OrdinalIgnoreCase));   // the list asks for something schema.sql never creates
            Assert.Empty(created.Except(expected, StringComparer.OrdinalIgnoreCase));   // schema.sql creates something the list does not know
        }

        [Fact]
        public void A_table_that_is_missing_is_named()
        {
            Data.Execute("EXEC sp_rename N'dbo.tbl_Announcement', N'tbl_Announcement_hidden';");
            try
            {
                string problem = Assert.Single(Verify().Problems);

                Assert.StartsWith("Table dbo.tbl_Announcement is missing", problem);
            }
            finally
            {
                Data.Execute("EXEC sp_rename N'dbo.tbl_Announcement_hidden', N'tbl_Announcement';");
            }
        }

        [Fact]
        public void A_table_of_the_right_name_in_another_schema_does_not_count()
        {
            // The application always says dbo.tbl_Announcement; a look-alike elsewhere must not hide that it is missing.
            Data.Execute("EXEC sp_rename N'dbo.tbl_Announcement', N'tbl_Announcement_hidden';");
            Data.Execute("CREATE SCHEMA elsewhere;");
            Data.Execute("CREATE TABLE elsewhere.tbl_Announcement (Id INT, Title NVARCHAR(150), Body NVARCHAR(2000), CreatedBy INT, CreatedOn DATETIME, ExpiresOn DATETIME);");
            try
            {
                string problem = Assert.Single(Verify().Problems);

                Assert.StartsWith("Table dbo.tbl_Announcement is missing", problem);
            }
            finally
            {
                Data.Execute("DROP TABLE elsewhere.tbl_Announcement;");
                Data.Execute("DROP SCHEMA elsewhere;");
                Data.Execute("EXEC sp_rename N'dbo.tbl_Announcement_hidden', N'tbl_Announcement';");
            }
        }

        [Fact]
        public void A_column_that_is_missing_is_named()
        {
            // A column no constraint refers to, so that SQL Server lets it be renamed.
            Data.Execute("EXEC sp_rename N'dbo.tbl_Room.Description', N'DescriptionHidden', N'COLUMN';");
            try
            {
                Assert.Equal(new[] { "Column dbo.tbl_Room.Description is missing." }, Verify().Problems);
            }
            finally
            {
                Data.Execute("EXEC sp_rename N'dbo.tbl_Room.DescriptionHidden', N'Description', N'COLUMN';");
            }
        }

        [Fact]
        public void A_login_without_the_data_roles_sees_no_tables_and_is_told_it_may_be_a_rights_problem()
        {
            // What the application would see if the login in connectionStrings.config was never given its rights.
            string user = DatabaseFixture.ApplicationLogin;
            Data.Execute("ALTER ROLE db_datareader DROP MEMBER " + user + "; ALTER ROLE db_datawriter DROP MEMBER " + user + ";");
            try
            {
                SchemaCheckResult result = Verify();

                Assert.Equal(ExpectedSchema.TablesAndColumns.Count(), result.Problems.Count);
                Assert.All(result.Problems, problem => Assert.Contains("no rights on it", problem));
            }
            finally
            {
                Data.Execute("ALTER ROLE db_datareader ADD MEMBER " + user + "; ALTER ROLE db_datawriter ADD MEMBER " + user + ";");
            }
        }

        [Fact]
        public void A_database_that_cannot_be_reached_is_a_logged_failure_and_not_an_empty_answer()
        {
            string login = DatabaseFixture.ApplicationLogin;
            SqlConnection.ClearAllPools();   // a pooled connection would keep working after the login is disabled
            Data.Execute("ALTER LOGIN " + login + " DISABLE;");
            try
            {
                Assert.Throws<DataAccessException>(() => Verify());

                Assert.Single(Log.Errors);
            }
            finally
            {
                Data.Execute("ALTER LOGIN " + login + " ENABLE;");
                // ADO.NET's pool remembers a failed login for a few seconds and answers every later attempt with the same
                // error, without asking the server, even though the login works again. Forgetting it here keeps this test
                // from failing whichever test happens to run next.
                SqlConnection.ClearAllPools();
            }
        }
    }
}
