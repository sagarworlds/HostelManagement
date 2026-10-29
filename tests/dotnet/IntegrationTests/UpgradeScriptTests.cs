using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// Database/upgrade-existing-db.sql run against a database shaped like the one the first version of the application
    /// used (OldSchemas/original-app.sql), with data that has the problems the script says it repairs or reports.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class UpgradeScriptTests : RepositoryTestBase, IDisposable
    {
        private const string OldPasswordColumn = "[Password] VARCHAR(50)        NOT NULL";

        public UpgradeScriptTests(DatabaseFixture database) : base(database)
        {
        }

        /// <summary>Leaves a normal schema behind for whichever test class runs next.</summary>
        public void Dispose()
        {
            Database.Rebuild();
        }

        // ---- building the old database ----

        /// <summary>Ids of the rows planted in the old database; each room and request illustrates one situation.</summary>
        private sealed class OldHostel
        {
            public int Admin, Student1, Student2, Student3, Student4, Student5;
            public int RoomFreeNull, RoomFreeOne, RoomHeldNull, RoomHeldWrong, RoomBlocked, RoomDouble;
            public int Student1Approved, Student1Pending, NoRoomApproved;
        }

        private const string Student1Password = "student one's password";

        private void CreateOldDatabase(string passwordColumn = OldPasswordColumn)
        {
            string script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "OldSchemas", "original-app.sql"));
            Assert.Contains(OldPasswordColumn, script); // keeps the variant replacement honest
            Database.Rebuild(script.Replace(OldPasswordColumn, passwordColumn));
        }

        private int OldUser(string userType, string email, string first, string password)
        {
            return Data.Scalar<int>(
                "INSERT INTO dbo.tbl_User (Email, [Password], UserType, FirstName, LastName, MobileNo, CreatedOn, ModifiedOn) " +
                "OUTPUT INSERTED.Id VALUES (@Email, @Hash, @Type, @First, N'Old', N'0700', GETDATE(), GETDATE());",
                new SqlParameter("@Email", email), new SqlParameter("@Hash", LegacyHash(password)),
                new SqlParameter("@Type", userType), new SqlParameter("@First", first));
        }

        private int OldRoom(string name, bool? availability)
        {
            return Data.Scalar<int>(
                "INSERT INTO dbo.tbl_Room (RoomName, Rent, [Description], Availability, CreatedOn, ModifiedOn) " +
                "OUTPUT INSERTED.Id VALUES (@Name, 300, N'old room', @Availability, GETDATE(), GETDATE());",
                new SqlParameter("@Name", name), new SqlParameter("@Availability", (object)availability ?? DBNull.Value));
        }

        private int OldRequest(int userId, string status, int? roomId)
        {
            return Data.Scalar<int>(
                "INSERT INTO dbo.tbl_RequestRoom (UserId, ApprovedRoomId, [Status], CreatedOn, RequestDate) " +
                "OUTPUT INSERTED.Id VALUES (@User, @Room, @Status, GETDATE(), GETDATE());",
                new SqlParameter("@User", userId), new SqlParameter("@Room", (object)roomId ?? DBNull.Value), new SqlParameter("@Status", status));
        }

        private OldHostel PlantOldData(bool duplicateEmail = false)
        {
            var h = new OldHostel();
            h.Admin = OldUser("Admin", "admin@old.test", "Admin", "admin password");
            h.Student1 = OldUser("Student", "s1@old.test", "One", Student1Password);
            h.Student2 = OldUser("Student", "s2@old.test", "Two", "two");
            h.Student3 = OldUser("Student", "s3@old.test", "Three", "three");
            h.Student4 = OldUser("Student", "s4@old.test", "Four", "four");
            h.Student5 = OldUser("Student", duplicateEmail ? "S1@OLD.TEST" : "s5@old.test", "Five", "five");

            h.RoomFreeNull = OldRoom("Free, never set", null);       // old versions never set Availability: invisible to students
            h.RoomFreeOne = OldRoom("Free, set", true);
            h.RoomHeldNull = OldRoom("Held, never set", null);       // allocated, flag missing
            h.RoomHeldWrong = OldRoom("Held, offered again", true);  // allocated, but still offered
            h.RoomBlocked = OldRoom("Blocked by hand", false);       // not allocated: someone switched it off on purpose
            h.RoomDouble = OldRoom("Double booked", true);           // two students approved into it

            h.Student1Approved = OldRequest(h.Student1, "Approved", h.RoomHeldNull);
            h.Student1Pending = OldRequest(h.Student1, "Pending", null);   // a second active request for the same student
            OldRequest(h.Student2, "Approved", h.RoomHeldWrong);
            OldRequest(h.Student3, "Approved", h.RoomDouble);
            OldRequest(h.Student4, "Approved", h.RoomDouble);
            h.NoRoomApproved = OldRequest(h.Student5, "Approved", null);   // approved with no room chosen
            OldRequest(h.Student2, "Rejected", null);                      // history
            return h;
        }

        // ---- inspecting ----

        private System.Collections.Generic.List<DataTable> RunUpgrade()
        {
            return SqlScript.Run(Database.DatabaseConnectionString,
                SqlScript.Retarget(DatabaseFixture.ReadDatabaseScript("upgrade-existing-db.sql"), DatabaseFixture.TestDatabase));
        }

        /// <summary>One string covering every column, index, default, key and check, plus a checksum of each table's rows.</summary>
        private string Snapshot()
        {
            string schema = Data.Scalar<string>(
                "SELECT STRING_AGG(x, N';') WITHIN GROUP (ORDER BY x) FROM (" +
                "  SELECT N'C:' + t.name + N'.' + c.name + N':' + ty.name + N':' + CAST(c.max_length AS NVARCHAR(10)) + N':' + CAST(c.is_nullable AS NVARCHAR(1)) AS x " +
                "  FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id " +
                "  UNION ALL SELECT N'I:' + i.name + N':' + CAST(i.is_unique AS NVARCHAR(1)) FROM sys.indexes i " +
                "    WHERE i.name IS NOT NULL AND OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1 " +
                "  UNION ALL SELECT N'D:' + name + N':' + definition FROM sys.default_constraints " +
                "  UNION ALL SELECT N'K:' + name + N':' + CAST(delete_referential_action AS NVARCHAR(2)) FROM sys.foreign_keys " +
                "  UNION ALL SELECT N'X:' + name + N':' + definition FROM sys.check_constraints) s;");

            var data = new System.Text.StringBuilder(schema);
            foreach (string table in new[] { "tbl_User", "tbl_Room", "tbl_RequestRoom", "tbl_PasswordReset", "tbl_Announcement" })
                data.Append("|" + table + "=" + Data.Scalar<int?>("SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo." + table + ";"));
            return data.ToString();
        }

        private static DataTable ResultWith(System.Collections.Generic.IEnumerable<DataTable> results, string column)
        {
            return results.Single(t => t.Columns.Contains(column));
        }

        private bool IndexExists(string name)
        {
            return Data.Scalar<int>("SELECT COUNT(1) FROM sys.indexes WHERE name = @Name;", new SqlParameter("@Name", name)) > 0;
        }

        private string ColumnType(string table, string column)
        {
            return Data.Scalar<string>(
                "SELECT DATA_TYPE + N'(' + CAST(CHARACTER_MAXIMUM_LENGTH AS NVARCHAR(10)) + N')' + CASE IS_NULLABLE WHEN 'YES' THEN N' NULL' ELSE N' NOT NULL' END " +
                "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @T AND COLUMN_NAME = @C;",
                new SqlParameter("@T", table), new SqlParameter("@C", column));
        }

        private bool Available(int roomId)
        {
            return Data.Scalar<bool>("SELECT Availability FROM dbo.tbl_Room WHERE Id = @Id;", new SqlParameter("@Id", roomId));
        }

        // ---- the upgrade itself ----

        [Fact]
        public void The_upgrade_adds_what_the_application_reads_and_keeps_every_existing_row()
        {
            CreateOldDatabase();
            PlantOldData();
            int usersBefore = Data.Count("dbo.tbl_User");
            int requestsBefore = Data.Count("dbo.tbl_RequestRoom");
            int roomsBefore = Data.Count("dbo.tbl_Room");

            RunUpgrade();

            Assert.Equal(usersBefore, Data.Count("dbo.tbl_User"));
            Assert.Equal(roomsBefore, Data.Count("dbo.tbl_Room"));
            Assert.Equal(requestsBefore, Data.Count("dbo.tbl_RequestRoom"));

            // new columns, with the values existing rows should get
            Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_User WHERE MustChangePassword = 1;"));
            Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE PreferredRoomId IS NOT NULL;"));
            // the password column now holds a current hash
            Assert.Equal("nvarchar(256) NOT NULL", ColumnType("tbl_User", "Password"));
            // new tables
            Assert.Equal(0, Data.Count("dbo.tbl_PasswordReset"));
            Assert.Equal(0, Data.Count("dbo.tbl_Announcement"));
            // supporting indexes and the key for the preferred room
            Assert.True(IndexExists("IX_tbl_RequestRoom_UserId_Status"));
            Assert.True(IndexExists("IX_tbl_RequestRoom_ApprovedRoomId"));
            Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM sys.foreign_keys WHERE name = N'FK_tbl_RequestRoom_PreferredRoom';"));
        }

        [Fact]
        public void The_upgrade_corrects_room_availability_without_overriding_a_deliberate_block()
        {
            CreateOldDatabase();
            OldHostel old = PlantOldData();

            RunUpgrade();

            Assert.True(Available(old.RoomFreeNull));    // was NULL, nobody holds it: now offered
            Assert.True(Available(old.RoomFreeOne));
            Assert.False(Available(old.RoomHeldNull));   // was NULL but held
            Assert.False(Available(old.RoomHeldWrong));  // was offered while held
            Assert.False(Available(old.RoomDouble));
            Assert.False(Available(old.RoomBlocked));    // switched off by hand and not allocated: left as it was
            // and a room created from now on is offered without the application having to say so
            Data.Execute("INSERT INTO dbo.tbl_Room (RoomName, Rent, CreatedOn, ModifiedOn) VALUES (N'New', 1, GETDATE(), GETDATE());");
            Assert.True(Data.Scalar<bool>("SELECT Availability FROM dbo.tbl_Room WHERE RoomName = N'New';"));
        }

        [Fact]
        public void The_upgrade_reports_double_bookings_and_other_problems_it_cannot_decide_for_itself()
        {
            CreateOldDatabase();
            OldHostel old = PlantOldData();

            var results = RunUpgrade();

            DataTable doubleBooked = ResultWith(results, "ActiveAllocations");
            Assert.Equal(new[] { old.RoomDouble }, doubleBooked.Rows.Cast<DataRow>().Select(r => (int)r["RoomId"]).ToArray());
            Assert.Equal(2, (int)doubleBooked.Rows[0]["ActiveAllocations"]);

            DataTable manyRequests = ResultWith(results, "ActiveRequests");
            Assert.Equal(new[] { old.Student1 }, manyRequests.Rows.Cast<DataRow>().Select(r => (int)r["UserId"]).ToArray());

            DataTable noRoom = ResultWith(results, "RequestId");
            Assert.Equal(new[] { old.NoRoomApproved }, noRoom.Rows.Cast<DataRow>().Select(r => (int)r["RequestId"]).ToArray());
        }

        [Fact]
        public void Running_the_upgrade_again_changes_nothing()
        {
            CreateOldDatabase();
            PlantOldData();
            RunUpgrade();
            string afterFirst = Snapshot();

            RunUpgrade();

            Assert.Equal(afterFirst, Snapshot());
        }

        [Fact]
        public void Upgrading_a_database_that_schema_sql_just_built_changes_nothing()
        {
            Database.Rebuild(); // schema.sql, as for someone who installed the current version and runs the upgrade anyway
            Data.AddStudent("keep@example.test");
            string before = Snapshot();

            RunUpgrade();

            Assert.Equal(before, Snapshot());
        }

        [Fact]
        public void A_nullable_password_column_is_widened_and_stays_nullable()
        {
            CreateOldDatabase("[Password] NVARCHAR(100)       NULL");

            RunUpgrade();

            Assert.Equal("nvarchar(256) NULL", ColumnType("tbl_User", "Password"));
        }

        [Fact]
        public void A_password_column_that_is_already_wide_enough_is_left_alone()
        {
            CreateOldDatabase("[Password] NVARCHAR(MAX)       NOT NULL");

            RunUpgrade();

            Assert.Equal("nvarchar(-1) NOT NULL", ColumnType("tbl_User", "Password"));
        }

        // ---- the application on the upgraded database ----

        [Fact]
        public void Students_of_the_old_database_sign_in_and_their_hashes_are_upgraded_in_the_wider_column()
        {
            CreateOldDatabase();
            OldHostel old = PlantOldData();
            RunUpgrade();
            var users = new UserDB(Log);
            string before = Data.Scalar<string>("SELECT [Password] FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", old.Student1));
            Assert.Equal(48, before.Length);

            User signedIn = users.Authenticate("s1@old.test", Student1Password);

            Assert.NotNull(signedIn);
            Assert.False(signedIn.MustChangePassword);
            string after = Data.Scalar<string>("SELECT [Password] FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", old.Student1));
            Assert.StartsWith("v2$", after);
            Assert.NotNull(users.Authenticate("s1@old.test", Student1Password));
            Assert.Null(users.Authenticate("s1@old.test", "not the password"));
            AssertNothingLogged();
        }

        [Fact]
        public void Every_part_of_the_application_works_on_the_upgraded_database()
        {
            CreateOldDatabase();
            OldHostel old = PlantOldData();
            RunUpgrade();
            var users = new UserDB(Log);
            var rooms = new RoomDB(Log);
            var requests = new RequestRoomDB(Log);

            // rooms that used to be invisible are offered; held ones are not
            string[] offered = rooms.GetAvailableRooms().Select(r => r.RoomName).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "Free, never set", "Free, set" }, offered);
            // the double-booked room is listed once for each student holding it, so the administrator can see the problem
            Assert.Equal(7, rooms.GetRoomList().Count);
            Assert.Equal(6, new DashboardDB(Log).GetSummary().TotalRooms);

            // a new student registers, asks for a room that used to be invisible, and is approved, then leaves
            Assert.True(users.AddUser(new User { Email = "new@old.test", Password = "a brand new passphrase", UserType = UserTypes.Student, FirstName = "New", LastName = "Student" }).Success);
            int newStudent = users.GetUserByEmail("new@old.test").Id;
            Assert.True(requests.AddRoomRequest(new RequestRoom { UserId = newStudent, PreferredRoomId = old.RoomFreeNull }).Success);
            RequestRoom pending = requests.GetRoomRequestListByUserId(newStudent).Single();
            Assert.Equal("Free, never set", pending.PreferredRoomName);
            Assert.True(requests.ApproveRequest(pending.Id, old.RoomFreeNull, old.Admin).Success);
            Assert.True(users.AddUser(new User { Email = "late@old.test", Password = "a brand new passphrase", UserType = UserTypes.Student, FirstName = "Late", LastName = "Student" }).Success);
            int lateStudent = users.GetUserByEmail("late@old.test").Id;
            Assert.True(requests.AddRoomRequest(new RequestRoom { UserId = lateStudent }).Success);
            Assert.False(requests.ApproveRequest(requests.GetRoomRequestListByUserId(lateStudent).Single().Id, old.RoomFreeNull, old.Admin).Success); // already taken
            Assert.True(requests.VacateRoom(pending.Id, old.Admin).Success);
            Assert.True(rooms.GetAvailableRooms().Any(r => r.Id == old.RoomFreeNull));

            // the student who held a room can be removed and the room comes back
            Assert.True(users.DeleteStudent(old.Student1).Success);
            Assert.True(Available(old.RoomHeldNull));

            // password reset and announcements
            var resets = new PasswordResetDB(Log);
            string hash = SecureToken.Sha256Hex("token");
            Assert.True(resets.CreateToken(newStudent, hash, DateTime.Now, DateTime.Now.AddHours(1)).Success);
            Assert.True(resets.CompleteReset(hash, PasswordUtility.HashPassword("another passphrase", 1000), DateTime.Now).Success);
            var announcements = new AnnouncementDB(Log);
            Assert.True(announcements.Add(new Announcement { Title = "Hello", Body = "Welcome back", CreatedOn = DateTime.Now }).Success);
            Assert.Single(announcements.GetActive(5, DateTime.Now));

            AssertNothingLogged();
        }

        [Fact]
        public void An_e_mail_address_can_no_longer_be_used_twice_once_the_database_is_upgraded()
        {
            CreateOldDatabase();
            PlantOldData();
            RunUpgrade();
            var users = new UserDB(Log);

            OperationResult duplicate = users.AddUser(new User { Email = "S1@old.test", Password = "a brand new passphrase", UserType = UserTypes.Student, FirstName = "Copy", LastName = "Cat" });

            Assert.False(duplicate.Success);
            Assert.Contains("already exists", duplicate.Message);
        }

        [Fact]
        public void Existing_duplicate_e_mail_addresses_are_reported_and_do_not_stop_the_upgrade()
        {
            CreateOldDatabase();
            PlantOldData(duplicateEmail: true);

            var results = RunUpgrade();

            DataTable duplicates = ResultWith(results, "Accounts");
            Assert.Single(duplicates.Rows);
            Assert.Equal(2, (int)duplicates.Rows[0]["Accounts"]);
            Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM sys.tables WHERE name = N'tbl_PasswordReset';")); // later steps still ran
        }
    }
}
