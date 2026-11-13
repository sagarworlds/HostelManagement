using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// Database/create-app-login.sql: what the login it creates can and cannot do, and what repeating the script does.
    /// (The rest of this test project runs entirely as a login made by this script, which shows the privileges suffice;
    /// these tests show they are no more than that.)
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class AppLoginScriptTests : RepositoryTestBase, IDisposable
    {
        // A login of its own, so these tests never disturb the one the other tests' repositories run as.
        private const string Login = "hostelhub_app_scripttest";
        private const string FirstPassword = "First!Pass-Word-93kd";
        private const string SecondPassword = "Second!Pass-Word-17xq";

        private const int LoginFailed = 18456;
        private const int CannotOpenDatabase = 4060;
        // The same refusal has a different number before SQL Server 2022 (15118) and from it (33064).
        private static readonly int[] PasswordPolicyFailures = { 15118, 33064 };

        public AppLoginScriptTests(DatabaseFixture database) : base(database)
        {
        }

        public void Dispose()
        {
            // The database user goes with a rebuild; the server-level login has to be dropped by name.
            Database.Rebuild();
            Database.DropLogin(Login);
        }

        private void RunScript(string password, string login = Login)
        {
            SqlScript.Run(Database.ServerConnectionString, DatabaseFixture.ApplicationLoginScript(login, password));
        }

        private string ConnectionString(string password)
        {
            // Pooling off: a pooled connection opened before a password change would keep working and hide the change.
            return new SqlConnectionStringBuilder(Database.DatabaseConnectionString) { UserID = Login, Password = password, Pooling = false }.ConnectionString;
        }

        private static void ExecuteAs(string connectionString, string sql)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static int Number(SqlException ex)
        {
            return ex.Number;
        }

        private List<string> RolesOfUser()
        {
            var roles = new List<string>();
            using (var connection = new SqlConnection(Database.DatabaseConnectionString))
            using (var command = new SqlCommand(
                "SELECT r.name FROM sys.database_role_members m " +
                "INNER JOIN sys.database_principals r ON r.principal_id = m.role_principal_id " +
                "INNER JOIN sys.database_principals u ON u.principal_id = m.member_principal_id " +
                "WHERE u.name = @Login ORDER BY r.name;", connection))
            {
                command.Parameters.AddWithValue("@Login", Login);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        roles.Add(reader.GetString(0));
                }
            }

            return roles;
        }

        // ---- what the login can do ----

        [Fact]
        public void The_login_can_read_and_write_every_table_and_take_a_lock()
        {
            RunScript(FirstPassword);
            string connection = ConnectionString(FirstPassword);

            foreach (string table in new[] { "tbl_User", "tbl_Room", "tbl_RequestRoom", "tbl_PasswordReset", "tbl_Announcement" })
                ExecuteAs(connection, "SELECT COUNT(1) FROM dbo." + table + ";");

            ExecuteAs(connection,
                "INSERT INTO dbo.tbl_Announcement (Title, Body, CreatedOn) VALUES (N't', N'b', GETDATE()); " +
                "UPDATE dbo.tbl_Announcement SET Title = N'u'; DELETE FROM dbo.tbl_Announcement;");
            ExecuteAs(connection,
                "BEGIN TRAN; DECLARE @r INT; EXEC @r = sys.sp_getapplock @Resource = N'x', @LockMode = N'Exclusive', " +
                "@LockOwner = N'Transaction', @LockTimeout = 1000; IF @r < 0 THROW 50000, 'no lock', 1; COMMIT;");
        }

        [Fact]
        public void The_login_has_exactly_the_two_data_roles()
        {
            RunScript(FirstPassword);

            Assert.Equal(new[] { "db_datareader", "db_datawriter" }, RolesOfUser());
        }

        [Theory]
        [InlineData("CREATE TABLE dbo.sneaky (a INT)")]
        [InlineData("ALTER TABLE dbo.tbl_User ADD sneaky INT")]
        [InlineData("DROP TABLE dbo.tbl_Announcement")]
        [InlineData("TRUNCATE TABLE dbo.tbl_Announcement")]
        [InlineData("CREATE INDEX ix_sneaky ON dbo.tbl_Room (Rent)")]
        [InlineData("CREATE USER sneaky WITHOUT LOGIN")]
        [InlineData("ALTER ROLE db_owner ADD MEMBER hostelhub_app_scripttest")]
        public void The_login_cannot_change_the_schema_or_its_own_rights(string statement)
        {
            RunScript(FirstPassword);

            Assert.Throws<SqlException>(() => ExecuteAs(ConnectionString(FirstPassword), statement));
        }

        [Fact]
        public void The_login_has_no_rights_at_server_level()
        {
            RunScript(FirstPassword);
            string connection = ConnectionString(FirstPassword);

            Assert.Throws<SqlException>(() => ExecuteAs(connection, "CREATE LOGIN sneaky WITH PASSWORD = 'Sneaky!Pass-Word-12ab'"));
            Assert.Throws<SqlException>(() => ExecuteAs(connection, "CREATE DATABASE sneaky"));
            Assert.Throws<SqlException>(() => ExecuteAs(connection, "BACKUP DATABASE " + DatabaseFixture.TestDatabase + " TO DISK = N'/var/opt/mssql/data/sneaky.bak'"));
        }

        // ---- repeating the script ----

        [Fact]
        public void Running_it_again_with_a_new_password_changes_the_password()
        {
            RunScript(FirstPassword);
            ExecuteAs(ConnectionString(FirstPassword), "SELECT 1");

            RunScript(SecondPassword);

            Assert.Equal(LoginFailed, Number(Assert.Throws<SqlException>(() => ExecuteAs(ConnectionString(FirstPassword), "SELECT 1"))));
            ExecuteAs(ConnectionString(SecondPassword), "SELECT 1");
            Assert.Equal(new[] { "db_datareader", "db_datawriter" }, RolesOfUser());
        }

        [Fact]
        public void Roles_given_by_hand_are_taken_away_again()
        {
            RunScript(FirstPassword);
            Data.Execute("ALTER ROLE db_owner ADD MEMBER " + Login + "; ALTER ROLE db_ddladmin ADD MEMBER " + Login + ";");
            Assert.Contains("db_owner", RolesOfUser());

            RunScript(FirstPassword);

            Assert.Equal(new[] { "db_datareader", "db_datawriter" }, RolesOfUser());
            Assert.Throws<SqlException>(() => ExecuteAs(ConnectionString(FirstPassword), "CREATE TABLE dbo.sneaky (a INT)"));
        }

        [Fact]
        public void A_database_user_left_behind_by_a_restore_is_connected_to_the_login_again()
        {
            RunScript(FirstPassword);
            // A restored copy keeps its user, but on the new server the login of that name is a different login (new SID).
            Database.DropLogin(Login);
            Data.Execute("CREATE LOGIN " + Login + " WITH PASSWORD = 'Orphan!Pass-Word-55zz', DEFAULT_DATABASE = " + DatabaseFixture.TestDatabase + ";");
            Assert.Equal(CannotOpenDatabase, Number(Assert.Throws<SqlException>(() => ExecuteAs(ConnectionString("Orphan!Pass-Word-55zz"), "SELECT 1"))));

            RunScript(SecondPassword);

            ExecuteAs(ConnectionString(SecondPassword), "SELECT COUNT(1) FROM dbo.tbl_User");
            Assert.Equal(new[] { "db_datareader", "db_datawriter" }, RolesOfUser());
        }

        [Fact]
        public void A_password_with_a_quote_in_it_works()
        {
            const string withQuote = "Quote!d-Pass'word-88qq";

            RunScript(withQuote);   // the helper doubles the quote, as a person must when typing it into the script

            ExecuteAs(ConnectionString(withQuote), "SELECT 1");
        }

        // ---- refusals ----

        [Fact]
        public void It_refuses_the_placeholder_even_when_someone_uses_Replace_All_on_it()
        {
            string script = DatabaseFixture.ReadDatabaseScript("create-app-login.sql");
            script = SqlScript.WithDeclaredValue(script, "LoginName", Login);

            // Untouched: still the placeholder.
            SqlException untouched = Assert.Throws<SqlException>(() => SqlScript.Run(Database.ServerConnectionString, SqlScript.Retarget(script, DatabaseFixture.TestDatabase)));
            Assert.Equal(50000, untouched.Number);

            // "Replace All" of the placeholder text with a real password must not be defeated by the check itself.
            string replacedEverywhere = script.Replace("PASTE_PASSWORD_HERE", FirstPassword);
            SqlScript.Run(Database.ServerConnectionString, SqlScript.Retarget(replacedEverywhere, DatabaseFixture.TestDatabase));
            ExecuteAs(ConnectionString(FirstPassword), "SELECT 1");
        }

        [Fact]
        public void It_refuses_a_short_password()
        {
            SqlException ex = Assert.Throws<SqlException>(() => RunScript("Short!1a"));

            Assert.Equal(50001, ex.Number);
        }

        [Fact]
        public void It_refuses_a_password_that_SQL_Server_considers_weak()
        {
            SqlException ex = Assert.Throws<SqlException>(() => RunScript("abcdefghijkl"));

            Assert.Contains(ex.Number, PasswordPolicyFailures);
        }

        [Fact]
        public void It_refuses_to_run_in_a_system_database()
        {
            string script = DatabaseFixture.ApplicationLoginScript(Login, FirstPassword).Replace(DatabaseFixture.TestDatabase, "master");

            SqlException ex = Assert.Throws<SqlException>(() => SqlScript.Run(Database.ServerConnectionString, script));

            Assert.Equal(50002, ex.Number);
        }
    }
}
