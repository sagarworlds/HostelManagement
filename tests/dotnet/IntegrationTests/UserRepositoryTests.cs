using System;
using System.Data.SqlClient;
using System.Linq;
using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    [Collection(DatabaseCollection.Name)]
    public class UserRepositoryTests : RepositoryTestBase
    {
        private const string Password = "correct horse battery";

        private readonly UserDB _users;

        public UserRepositoryTests(DatabaseFixture database) : base(database)
        {
            _users = new UserDB(Log);
        }

        private User NewStudent(string email = "sam@example.test", string first = "Sam", string last = "Student")
        {
            return new User { Email = email, Password = Password, UserType = UserTypes.Student, FirstName = first, LastName = last, MobileNo = "07700 900123" };
        }

        private string StoredHash(int userId)
        {
            return Data.Scalar<string>("SELECT [Password] FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", userId));
        }

        // ---- AddUser / Authenticate ----

        [Fact]
        public void AddUser_stores_a_current_hash_never_the_password_and_the_account_can_sign_in()
        {
            OperationResult result = _users.AddUser(NewStudent());

            Assert.True(result.Success, result.Message);
            string stored = Data.Scalar<string>("SELECT [Password] FROM dbo.tbl_User WHERE Email = N'sam@example.test';");
            Assert.StartsWith("v2$" + PasswordUtility.CurrentIterations + "$", stored);
            Assert.DoesNotContain(Password, stored);

            User signedIn = _users.Authenticate("sam@example.test", Password);
            Assert.NotNull(signedIn);
            Assert.Equal("Sam Student", signedIn.UserName);
            Assert.Equal(UserTypes.Student, signedIn.UserType);
            Assert.False(signedIn.MustChangePassword);
        }

        [Fact]
        public void AddUser_does_not_change_the_callers_user_object()
        {
            User user = NewStudent();

            _users.AddUser(user);

            Assert.Equal(Password, user.Password);
        }

        [Fact]
        public void AddUser_refuses_a_second_account_with_the_same_email_ignoring_case()
        {
            Assert.True(_users.AddUser(NewStudent("sam@example.test")).Success);

            OperationResult duplicate = _users.AddUser(NewStudent("SAM@Example.Test", "Other", "Person"));

            Assert.False(duplicate.Success);
            Assert.Contains("already exists", duplicate.Message);
            Assert.Equal(1, Data.Count("dbo.tbl_User"));
            AssertNothingLogged(); // an expected refusal is not an error worth logging
        }

        [Fact]
        public void AddUser_round_trips_non_ascii_names()
        {
            Assert.True(_users.AddUser(NewStudent("zoe@example.test", "Zoë", "李雷")).Success);

            User stored = _users.GetUserByEmail("zoe@example.test");

            Assert.Equal("Zoë", stored.FirstName);
            Assert.Equal("李雷", stored.LastName);
        }

        [Fact]
        public void AddUser_stores_text_that_looks_like_SQL_as_plain_text()
        {
            const string hostile = "Robert'); DROP TABLE dbo.tbl_User;--";

            Assert.True(_users.AddUser(NewStudent("bobby@example.test", hostile, "Tables")).Success);

            Assert.Equal(hostile, _users.GetUserByEmail("bobby@example.test").FirstName);
            Assert.Equal(1, Data.Count("dbo.tbl_User"));
        }

        [Fact]
        public void AddUser_accepts_a_missing_mobile_number()
        {
            User user = NewStudent();
            user.MobileNo = null;

            Assert.True(_users.AddUser(user).Success);
            Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_User WHERE MobileNo IS NULL;"));
        }

        [Fact]
        public void AddUser_rejects_an_unknown_account_type_through_the_check_constraint()
        {
            User user = NewStudent();
            user.UserType = "Janitor";

            OperationResult result = _users.AddUser(user);

            Assert.False(result.Success);
            Assert.Equal(0, Data.Count("dbo.tbl_User"));
            Assert.Single(Log.Errors);
        }

        [Fact]
        public void Authenticate_returns_null_for_an_unknown_email_and_for_a_wrong_password()
        {
            Data.AddStudent("sam@example.test");

            Assert.Null(_users.Authenticate("nobody@example.test", Password));
            Assert.Null(_users.Authenticate("sam@example.test", "wrong password"));
            Assert.NotNull(_users.Authenticate("sam@example.test", Password));
        }

        [Fact]
        public void Authenticate_finds_the_account_whatever_the_case_of_the_email()
        {
            Data.AddStudent("sam@example.test");

            Assert.NotNull(_users.Authenticate("SAM@EXAMPLE.TEST", Password));
        }

        [Fact]
        public void Authenticate_reports_that_a_password_change_is_required()
        {
            Data.AddUser(UserTypes.Student, "sam@example.test", mustChangePassword: true);

            Assert.True(_users.Authenticate("sam@example.test", Password).MustChangePassword);
        }

        [Fact]
        public void Authenticate_accepts_a_legacy_hash_and_replaces_it_with_a_current_one()
        {
            int id = Data.AddUser(UserTypes.Student, "old@example.test", rawPasswordHash: LegacyHash(Password));
            string legacy = StoredHash(id);
            Assert.True(PasswordUtility.NeedsRehash(legacy));

            Assert.NotNull(_users.Authenticate("old@example.test", Password));

            string upgraded = StoredHash(id);
            Assert.NotEqual(legacy, upgraded);
            Assert.False(PasswordUtility.NeedsRehash(upgraded));
            Assert.NotNull(_users.Authenticate("old@example.test", Password)); // and the new hash works
            AssertNothingLogged();
        }

        [Fact]
        public void Authenticate_leaves_a_legacy_hash_alone_when_the_password_is_wrong()
        {
            int id = Data.AddUser(UserTypes.Student, "old@example.test", rawPasswordHash: LegacyHash(Password));
            string before = StoredHash(id);

            Assert.Null(_users.Authenticate("old@example.test", "wrong password"));

            Assert.Equal(before, StoredHash(id));
        }

        [Fact]
        public void Authenticate_treats_a_damaged_stored_hash_as_a_failed_sign_in_and_logs_it()
        {
            Data.AddUser(UserTypes.Student, "damaged@example.test", rawPasswordHash: "!!! not a hash !!!");

            Assert.Null(_users.Authenticate("damaged@example.test", Password));

            Assert.Single(Log.Errors);
            Assert.Contains("malformed", Log.Errors[0]);
        }

        // ---- UpdateUser / UpdateProfile ----

        [Fact]
        public void UpdateUser_changes_name_and_email_but_not_password_type_or_mobile()
        {
            int id = Data.AddStudent("sam@example.test");
            string hashBefore = StoredHash(id);

            OperationResult result = _users.UpdateUser(new User { Id = id, Email = "sam.new@example.test", FirstName = "Samuel", LastName = "Changed", UserType = UserTypes.Student });

            Assert.True(result.Success, result.Message);
            User stored = _users.GetUserById(id);
            Assert.Equal("sam.new@example.test", stored.Email);
            Assert.Equal("Samuel Changed", stored.UserName);
            Assert.Equal("0000", stored.MobileNo);
            Assert.Equal(hashBefore, StoredHash(id));
        }

        [Fact]
        public void UpdateUser_does_nothing_when_the_account_type_does_not_match()
        {
            int id = Data.AddStudent();

            OperationResult result = _users.UpdateUser(new User { Id = id, Email = "x@example.test", FirstName = "X", LastName = "Y", UserType = UserTypes.Admin });

            Assert.False(result.Success);
            Assert.NotEqual("X", _users.GetUserById(id).FirstName);
        }

        [Fact]
        public void UpdateUser_refuses_an_email_that_belongs_to_another_account()
        {
            Data.AddStudent("taken@example.test");
            int id = Data.AddStudent("mine@example.test");

            OperationResult result = _users.UpdateUser(new User { Id = id, Email = "TAKEN@example.test", FirstName = "A", LastName = "B", UserType = UserTypes.Student });

            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message);
            Assert.Equal("mine@example.test", _users.GetUserById(id).Email);
        }

        [Fact]
        public void UpdateProfile_changes_name_and_mobile_only()
        {
            int id = Data.AddStudent("sam@example.test");

            OperationResult result = _users.UpdateProfile(new User { Id = id, FirstName = "Samantha", LastName = "Renamed", MobileNo = "07911 123456" });

            Assert.True(result.Success, result.Message);
            User stored = _users.GetUserById(id);
            Assert.Equal("Samantha Renamed", stored.UserName);
            Assert.Equal("07911 123456", stored.MobileNo);
            Assert.Equal("sam@example.test", stored.Email);
            Assert.Equal(UserTypes.Student, stored.UserType);
        }

        [Fact]
        public void UpdateProfile_reports_an_account_that_no_longer_exists()
        {
            Assert.False(_users.UpdateProfile(new User { Id = 987654, FirstName = "A", LastName = "B", MobileNo = "1" }).Success);
        }

        // ---- passwords ----

        [Fact]
        public void ChangePassword_with_the_right_current_password_sets_the_new_one_and_clears_the_flag()
        {
            int id = Data.AddUser(UserTypes.Student, "sam@example.test", mustChangePassword: true);

            OperationResult result = _users.ChangePassword(id, Password, "a brand new passphrase");

            Assert.True(result.Success, result.Message);
            Assert.Null(_users.Authenticate("sam@example.test", Password));
            User signedIn = _users.Authenticate("sam@example.test", "a brand new passphrase");
            Assert.NotNull(signedIn);
            Assert.False(signedIn.MustChangePassword);
        }

        [Fact]
        public void ChangePassword_with_the_wrong_current_password_changes_nothing()
        {
            int id = Data.AddStudent("sam@example.test");
            string before = StoredHash(id);

            OperationResult result = _users.ChangePassword(id, "not my password", "a brand new passphrase");

            Assert.False(result.Success);
            Assert.Contains("incorrect", result.Message);
            Assert.Equal(before, StoredHash(id));
        }

        [Fact]
        public void ChangePassword_reports_an_account_that_no_longer_exists()
        {
            Assert.False(_users.ChangePassword(987654, Password, "a brand new passphrase").Success);
        }

        [Fact]
        public void ResetStudentPassword_sets_a_temporary_password_and_requires_a_change()
        {
            int id = Data.AddStudent("sam@example.test");

            OperationResult result = _users.ResetStudentPassword(id, "Temp-Passw0rd-xyz");

            Assert.True(result.Success, result.Message);
            Assert.Null(_users.Authenticate("sam@example.test", Password));
            Assert.True(_users.Authenticate("sam@example.test", "Temp-Passw0rd-xyz").MustChangePassword);
        }

        [Fact]
        public void ResetStudentPassword_refuses_administrators_and_unknown_accounts()
        {
            int adminId = Data.AddAdmin("admin@example.test");

            Assert.False(_users.ResetStudentPassword(adminId, "Temp-Passw0rd-xyz").Success);
            Assert.False(_users.ResetStudentPassword(987654, "Temp-Passw0rd-xyz").Success);
            Assert.NotNull(_users.Authenticate("admin@example.test", Password)); // untouched
        }

        // ---- reads ----

        [Fact]
        public void EmailExists_ignores_case_and_the_account_being_edited()
        {
            int id = Data.AddStudent("sam@example.test");

            Assert.True(_users.EmailExists("SAM@example.test", 0));
            Assert.False(_users.EmailExists("sam@example.test", id));
            Assert.False(_users.EmailExists("other@example.test", 0));
        }

        [Fact]
        public void GetUsersByType_lists_only_that_type_ordered_by_name()
        {
            Data.AddStudent("c@example.test", "Carol", "Zed");
            Data.AddStudent("a@example.test", "Alice", "Young");
            Data.AddStudent("b@example.test", "Alice", "Adams");
            Data.AddAdmin("admin@example.test", "Zara", "Admin");

            var students = _users.GetUsersByType(UserTypes.Student);

            Assert.Equal(new[] { "Alice Adams", "Alice Young", "Carol Zed" }, students.Select(s => s.UserName).ToArray());
            Assert.Equal(new[] { "Zara Admin" }, _users.GetUsersByType(UserTypes.Admin).Select(s => s.UserName).ToArray());
        }

        [Fact]
        public void GetUserById_and_GetUserByEmail_return_null_for_unknown_accounts()
        {
            int id = Data.AddStudent("sam@example.test");

            Assert.Equal(id, _users.GetUserById(id).Id);
            Assert.Equal(id, _users.GetUserByEmail("SAM@example.test").Id);
            Assert.Null(_users.GetUserById(987654));
            Assert.Null(_users.GetUserByEmail("nobody@example.test"));
        }

        // ---- deleting ----

        [Fact]
        public void DeleteStudent_removes_the_account_requests_and_reset_links_and_frees_the_room()
        {
            int studentId = Data.AddStudent("leaving@example.test");
            int otherId = Data.AddStudent("staying@example.test");
            int roomId = Data.AddRoom("Held");
            Data.AllocateRoom(roomId, studentId);
            Data.AddRequest(studentId, RequestStatus.Rejected);
            Data.AddRequest(otherId, RequestStatus.Pending);
            Data.Execute("INSERT INTO dbo.tbl_PasswordReset (UserId, TokenHash, CreatedOn, ExpiresOn) VALUES (@Id, REPLICATE('a', 64), GETDATE(), DATEADD(HOUR, 1, GETDATE()));", new SqlParameter("@Id", studentId));

            OperationResult result = _users.DeleteStudent(studentId);

            Assert.True(result.Success, result.Message);
            Assert.Null(_users.GetUserById(studentId));
            Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE UserId = @Id;", new SqlParameter("@Id", studentId)));
            Assert.Equal(0, Data.Count("dbo.tbl_PasswordReset"));
            Assert.True(Data.IsAvailable(roomId));
            Assert.Equal(1, Data.Count("dbo.tbl_RequestRoom")); // the other student's request survives
            Assert.NotNull(_users.GetUserById(otherId));
        }

        [Fact]
        public void DeleteStudent_refuses_unknown_accounts_and_administrators()
        {
            int adminId = Data.AddAdmin();

            Assert.False(_users.DeleteStudent(987654).Success);
            Assert.False(_users.DeleteStudent(adminId).Success);
            Assert.NotNull(_users.GetUserById(adminId));
        }

        [Fact]
        public void DeleteAdmin_removes_another_administrator_when_one_remains()
        {
            int keep = Data.AddAdmin("keep@example.test");
            int remove = Data.AddAdmin("remove@example.test");

            OperationResult result = _users.DeleteAdmin(remove, keep);

            Assert.True(result.Success, result.Message);
            Assert.Null(_users.GetUserById(remove));
            Assert.NotNull(_users.GetUserById(keep));
        }

        [Fact]
        public void DeleteAdmin_refuses_the_signed_in_administrator()
        {
            int admin = Data.AddAdmin();
            Data.AddAdmin();

            OperationResult result = _users.DeleteAdmin(admin, admin);

            Assert.False(result.Success);
            Assert.NotNull(_users.GetUserById(admin));
        }

        [Fact]
        public void DeleteAdmin_refuses_to_remove_the_last_administrator()
        {
            int lastAdmin = Data.AddAdmin();
            int signedIn = Data.AddStudent(); // any other id: the rule is about the count of administrators

            OperationResult result = _users.DeleteAdmin(lastAdmin, signedIn);

            Assert.False(result.Success);
            Assert.NotNull(_users.GetUserById(lastAdmin));
        }

        [Fact]
        public void DeleteAdmin_does_not_delete_students()
        {
            Data.AddAdmin();
            Data.AddAdmin();
            int student = Data.AddStudent();

            Assert.False(_users.DeleteAdmin(student, 0).Success);
            Assert.NotNull(_users.GetUserById(student));
        }

        [Fact]
        public void Two_administrators_deleting_each_other_at_once_leave_exactly_one()
        {
            for (int round = 0; round < 15; round++)
            {
                Database.ClearAllData();
                int first = Data.AddAdmin();
                int second = Data.AddAdmin();

                var results = Concurrently.Run(
                    () => new UserDB(Log).DeleteAdmin(first, second),
                    () => new UserDB(Log).DeleteAdmin(second, first));

                Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_User WHERE UserType = N'Admin';"));
                Assert.Equal(1, results.Count(r => r.Success));
                AssertNothingLogged();
            }
        }
    }
}
