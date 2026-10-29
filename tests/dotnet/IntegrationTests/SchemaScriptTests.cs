using System;
using System.Data.SqlClient;
using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    /// <summary>The claims schema.sql and seed-admin.sql make, checked on a real server.</summary>
    [Collection(DatabaseCollection.Name)]
    public class SchemaScriptTests : RepositoryTestBase
    {
        private const int CheckOrForeignKeyViolation = 547;
        private const int UniqueViolation = 2627;

        public SchemaScriptTests(DatabaseFixture database) : base(database)
        {
        }

        private string Retargeted(string fileName)
        {
            return SqlScript.Retarget(DatabaseFixture.ReadDatabaseScript(fileName), DatabaseFixture.TestDatabase);
        }

        private SqlException ErrorOf(string sql, params SqlParameter[] parameters)
        {
            return Assert.Throws<SqlException>(() => Data.Execute(sql, parameters));
        }

        [Fact]
        public void Running_schema_sql_again_changes_neither_the_tables_nor_their_data()
        {
            int student = Data.AddStudent("keep@example.test");
            int tablesBefore = Data.Scalar<int>("SELECT COUNT(1) FROM sys.tables;");

            SqlScript.Run(Database.ServerConnectionString, Retargeted("schema.sql"));

            Assert.Equal(tablesBefore, Data.Scalar<int>("SELECT COUNT(1) FROM sys.tables;"));
            Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", student)));
        }

        [Fact]
        public void Only_known_account_types_and_request_statuses_are_accepted()
        {
            Assert.Equal(CheckOrForeignKeyViolation, ErrorOf(
                "INSERT INTO dbo.tbl_User (Email, [Password], UserType, FirstName, LastName, CreatedOn, ModifiedOn) VALUES (N'x@example.test', N'h', N'Janitor', N'A', N'B', GETDATE(), GETDATE());").Number);

            int student = Data.AddStudent();
            Assert.Equal(CheckOrForeignKeyViolation, ErrorOf(
                "INSERT INTO dbo.tbl_RequestRoom (UserId, [Status], CreatedOn, RequestDate) VALUES (@U, N'Maybe', GETDATE(), GETDATE());",
                new SqlParameter("@U", student)).Number);
        }

        [Fact]
        public void An_email_address_can_be_used_once_whatever_its_case()
        {
            Data.AddStudent("sam@example.test");

            Assert.Equal(UniqueViolation, ErrorOf(
                "INSERT INTO dbo.tbl_User (Email, [Password], UserType, FirstName, LastName, CreatedOn, ModifiedOn) VALUES (N'SAM@EXAMPLE.TEST', N'h', N'Student', N'A', N'B', GETDATE(), GETDATE());").Number);
        }

        [Fact]
        public void A_password_hash_of_the_current_length_fits_with_room_to_spare()
        {
            string hash = PasswordUtility.HashPassword("a long enough passphrase", 1000);
            Assert.True(hash.Length <= 128, "hash is " + hash.Length + " characters");

            int id = Data.AddUser(UserTypes.Student, rawPasswordHash: hash);

            Assert.Equal(hash, Data.Scalar<string>("SELECT [Password] FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", id)));
            Assert.Equal(256, Data.Scalar<int>("SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'tbl_User' AND COLUMN_NAME = N'Password';"));
        }

        [Fact]
        public void A_negative_rent_is_refused()
        {
            Assert.Equal(CheckOrForeignKeyViolation, ErrorOf(
                "INSERT INTO dbo.tbl_Room (RoomName, Rent, Availability, CreatedOn, ModifiedOn) VALUES (N'x', -1, 1, GETDATE(), GETDATE());").Number);
        }

        [Fact]
        public void Deleting_a_room_blanks_the_allocation_history_but_a_stated_preference_blocks_the_delete()
        {
            int student = Data.AddStudent();
            int allocatedRoom = Data.AddRoom("History");
            int preferredRoom = Data.AddRoom("Wanted");
            int past = Data.AddRequest(student, RequestStatus.Vacated, approvedRoomId: allocatedRoom);
            Data.AddRequest(Data.AddStudent(), RequestStatus.Pending, preferredRoomId: preferredRoom);

            Data.Execute("DELETE FROM dbo.tbl_Room WHERE Id = @Id;", new SqlParameter("@Id", allocatedRoom));

            Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE Id = @Id AND ApprovedRoomId IS NOT NULL;", new SqlParameter("@Id", past)));
            // RoomDB.DeleteRoom exists to clear this column first; the database itself refuses.
            Assert.Equal(CheckOrForeignKeyViolation, ErrorOf("DELETE FROM dbo.tbl_Room WHERE Id = @Id;", new SqlParameter("@Id", preferredRoom)).Number);
        }

        [Fact]
        public void A_user_with_requests_cannot_be_deleted_directly_but_their_reset_links_go_with_them()
        {
            int withRequest = Data.AddStudent();
            Data.AddRequest(withRequest);
            int withLink = Data.AddStudent();
            Data.Execute("INSERT INTO dbo.tbl_PasswordReset (UserId, TokenHash, CreatedOn, ExpiresOn) VALUES (@Id, REPLICATE('b', 64), GETDATE(), DATEADD(HOUR, 1, GETDATE()));", new SqlParameter("@Id", withLink));

            Assert.Equal(CheckOrForeignKeyViolation, ErrorOf("DELETE FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", withRequest)).Number);
            Data.Execute("DELETE FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", withLink));

            Assert.Equal(0, Data.Count("dbo.tbl_PasswordReset"));
        }

        // ---- seed-admin.sql ----

        [Fact]
        public void The_seed_script_refuses_to_run_until_a_hash_has_been_pasted_in()
        {
            SqlException ex = Assert.Throws<SqlException>(() => SqlScript.Run(Database.ServerConnectionString, Retargeted("seed-admin.sql")));

            Assert.Equal(50000, ex.Number);
            Assert.Equal(0, Data.Count("dbo.tbl_User"));
        }

        /// <summary>
        /// Output of the PowerShell snippet at the top of seed-admin.sql, run unchanged (PowerShell 7) with the password
        /// "seed-test-password" and confirmed independently with Python's hashlib. Keep it in step with that snippet.
        /// </summary>
        private const string DocumentedHash = "v2$600000$3G0rZfLrB9ux/I53j90J2g==$V49VZfWYPbzuB2fjrfkd6rIxwEBnnDyttlYpe3xGpCI=";

        [Fact]
        public void An_administrator_created_with_the_seed_script_can_sign_in_and_the_script_will_not_run_twice()
        {
            // Only the DECLARE line is edited by a user; the placeholder also appears in the script's own guard clause.
            string script = Retargeted("seed-admin.sql").Replace("N'PASTE_HASH_HERE';", "N'" + DocumentedHash + "';");

            SqlScript.Run(Database.ServerConnectionString, script);

            User admin = new UserDB(Log).Authenticate("admin@example.com", "seed-test-password");
            Assert.NotNull(admin);
            Assert.Equal(UserTypes.Admin, admin.UserType);
            Assert.False(PasswordUtility.NeedsRehash(DocumentedHash)); // already the current format: nothing to upgrade
            Assert.Equal(DocumentedHash, Data.Scalar<string>("SELECT [Password] FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", admin.Id)));

            SqlException second = Assert.Throws<SqlException>(() => SqlScript.Run(Database.ServerConnectionString, script));
            Assert.Equal(50001, second.Number);
            Assert.Equal(1, Data.Count("dbo.tbl_User"));
        }
    }
}
