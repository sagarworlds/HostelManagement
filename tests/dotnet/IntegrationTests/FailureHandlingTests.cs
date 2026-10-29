using System;
using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// What users and the log see when the database cannot do what was asked. The cause is staged by hiding a table,
    /// which is what a code-before-upgrade deployment looks like to the repositories.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class FailureHandlingTests : RepositoryTestBase
    {
        public FailureHandlingTests(DatabaseFixture database) : base(database)
        {
        }

        private void WithTableMissing(string table, Action action)
        {
            Data.Execute("EXEC sp_rename N'dbo." + table + "', N'" + table + "_hidden';");
            try
            {
                action();
            }
            finally
            {
                Data.Execute("EXEC sp_rename N'dbo." + table + "_hidden', N'" + table + "';");
            }
        }

        private void AssertReadFailsSafely(string table, Action read)
        {
            WithTableMissing(table, () =>
            {
                var ex = Assert.Throws<DataAccessException>(read);
                Assert.DoesNotContain("tbl_", ex.Message);   // nothing about the schema reaches the page
                Assert.IsAssignableFrom<System.Data.SqlClient.SqlException>(ex.InnerException);
            });

            Assert.Single(Log.Errors);
            Assert.Contains("run Database/upgrade-existing-db.sql", Log.Errors[0]);
        }

        private void AssertWriteFailsSafely(string table, Func<OperationResult> write)
        {
            WithTableMissing(table, () =>
            {
                OperationResult result = write();

                Assert.False(result.Success);
                Assert.DoesNotContain("tbl_", result.Message);
                Assert.DoesNotContain("Invalid object", result.Message);
                Assert.Contains("Something went wrong", result.Message);
            });

            Assert.Single(Log.Errors);
            Assert.Contains("run Database/upgrade-existing-db.sql", Log.Errors[0]);
        }

        [Fact]
        public void Reading_users_from_a_database_that_lacks_the_table_throws_and_points_to_the_upgrade_script()
        {
            AssertReadFailsSafely("tbl_User", () => new UserDB(Log).GetUserById(1));
        }

        [Fact]
        public void Signing_in_against_a_database_that_lacks_the_table_throws_rather_than_reporting_a_wrong_password()
        {
            AssertReadFailsSafely("tbl_User", () => new UserDB(Log).Authenticate("a@example.test", "secret"));
        }

        [Fact]
        public void Listing_rooms_fails_loudly()
        {
            AssertReadFailsSafely("tbl_Room", () => new RoomDB(Log).GetRoomList());
        }

        [Fact]
        public void Reading_requests_fails_loudly()
        {
            AssertReadFailsSafely("tbl_RequestRoom", () => new RequestRoomDB(Log).GetRoomRequestList(null));
        }

        [Fact]
        public void The_dashboard_fails_loudly()
        {
            AssertReadFailsSafely("tbl_Room", () => new DashboardDB(Log).GetSummary());
        }

        [Fact]
        public void Reading_announcements_fails_loudly()
        {
            AssertReadFailsSafely("tbl_Announcement", () => new AnnouncementDB(Log).GetAll());
        }

        [Fact]
        public void Checking_a_reset_link_fails_loudly()
        {
            AssertReadFailsSafely("tbl_PasswordReset", () => new PasswordResetDB(Log).IsTokenValid(SecureToken.Sha256Hex("x"), DateTime.Now));
        }

        [Fact]
        public void Adding_a_user_fails_with_a_plain_message()
        {
            AssertWriteFailsSafely("tbl_User", () => new UserDB(Log).AddUser(
                new User { Email = "a@example.test", Password = "secret passphrase", UserType = UserTypes.Student, FirstName = "A", LastName = "B" }));
        }

        [Fact]
        public void Adding_a_room_fails_with_a_plain_message()
        {
            AssertWriteFailsSafely("tbl_Room", () => new RoomDB(Log).AddRoom(new Room { RoomName = "A", Rent = 1 }));
        }

        [Fact]
        public void Requesting_a_room_fails_with_a_plain_message()
        {
            AssertWriteFailsSafely("tbl_RequestRoom", () => new RequestRoomDB(Log).AddRoomRequest(new RequestRoom { UserId = 1 }));
        }

        [Fact]
        public void Posting_an_announcement_fails_with_a_plain_message()
        {
            AssertWriteFailsSafely("tbl_Announcement", () => new AnnouncementDB(Log).Add(
                new Announcement { Title = "t", Body = "b", CreatedOn = DateTime.Now }));
        }

        [Fact]
        public void Creating_a_reset_link_fails_with_a_plain_message()
        {
            AssertWriteFailsSafely("tbl_PasswordReset", () => new PasswordResetDB(Log).CreateToken(
                1, SecureToken.Sha256Hex("x"), DateTime.Now, DateTime.Now.AddHours(1)));
        }

        [Fact]
        public void A_failed_approval_in_the_middle_of_a_transaction_changes_nothing()
        {
            int request = Data.AddRequest(Data.AddStudent());
            int room = Data.AddRoom("A-1");

            // The request update succeeds, then the room update cannot run: the first must be rolled back.
            WithTableMissing("tbl_Room", () => new RequestRoomDB(Log).ApproveRequest(request, room, 1));

            Assert.Equal(RequestStatus.Pending, Data.StatusOf(request));
            Assert.True(Data.IsAvailable(room));
        }
    }
}
