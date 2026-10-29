using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HostelManagement.Utility;

namespace HostelManagement
{
    /// <summary>SQL Server implementation of <see cref="IUserRepository"/>.</summary>
    public class UserDB : RepositoryBase, IUserRepository
    {
        public UserDB(ILogger logger) : base(logger)
        {
        }

        public OperationResult AddUser(User user)
        {
            // Hash into a local so the caller's User object is not silently changed.
            string passwordHash = PasswordUtility.HashPassword(user.Password);
            const string sql =
                "INSERT INTO dbo.tbl_User (Email, [Password], UserType, FirstName, LastName, MobileNo, CreatedOn, ModifiedOn) " +
                "VALUES (@Email, @Password, @UserType, @FirstName, @LastName, @MobileNo, @CreatedOn, @ModifiedOn);";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@Email", user.Email),
                        Param("@Password", passwordHash),
                        Param("@UserType", user.UserType),
                        Param("@FirstName", user.FirstName),
                        Param("@LastName", user.LastName),
                        Param("@MobileNo", user.MobileNo),
                        Param("@CreatedOn", user.CreatedOn),
                        Param("@ModifiedOn", user.ModifiedOn)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail(GenericWriteFailureMessage);
                }
            }
            catch (SqlException ex)
            {
                // The unique index also closes the race between EmailExists and this insert.
                if (IsUniqueViolation(ex))
                    return OperationResult.Fail("An account with this email address already exists.");

                return FailureResult("add user", ex);
            }
        }

        public OperationResult UpdateUser(User user)
        {
            const string sql =
                "UPDATE dbo.tbl_User SET Email = @Email, FirstName = @FirstName, LastName = @LastName, ModifiedOn = @ModifiedOn " +
                "WHERE Id = @Id AND UserType = @UserType;";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@Email", user.Email),
                        Param("@FirstName", user.FirstName),
                        Param("@LastName", user.LastName),
                        Param("@ModifiedOn", DateTime.Now),
                        Param("@Id", user.Id),
                        Param("@UserType", user.UserType)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail("This account no longer exists.");
                }
            }
            catch (SqlException ex)
            {
                if (IsUniqueViolation(ex))
                    return OperationResult.Fail("An account with this email address already exists.");

                return FailureResult("update user " + user.Id, ex);
            }
        }

        public OperationResult UpdateProfile(User user)
        {
            const string sql =
                "UPDATE dbo.tbl_User SET FirstName = @FirstName, LastName = @LastName, MobileNo = @MobileNo, ModifiedOn = @ModifiedOn " +
                "WHERE Id = @Id;";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@FirstName", user.FirstName),
                        Param("@LastName", user.LastName),
                        Param("@MobileNo", user.MobileNo),
                        Param("@ModifiedOn", DateTime.Now),
                        Param("@Id", user.Id)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail("Your account could not be found. Please sign in again.");
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("update profile " + user.Id, ex);
            }
        }

        public OperationResult ChangePassword(int userId, string currentPassword, string newPassword)
        {
            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(
                        "SELECT [Password] FROM dbo.tbl_User WHERE Id = @Id;", Params(Param("@Id", userId)));
                    if (table.Rows.Count == 0)
                        return OperationResult.Fail("Your account could not be found. Please sign in again.");

                    string storedHash = Convert.ToString(table.Rows[0]["Password"]);
                    if (!IsPasswordValid(currentPassword, storedHash, "user " + userId))
                        return OperationResult.Fail("The current password is incorrect.");

                    // Matching on the old hash makes this a compare-and-swap: if the password changed in another
                    // session between the read above and this update, nothing is overwritten.
                    int rows = db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_User SET [Password] = @NewHash, MustChangePassword = 0, ModifiedOn = @ModifiedOn " +
                        "WHERE Id = @Id AND [Password] = @OldHash;",
                        Params(
                            Param("@NewHash", PasswordUtility.HashPassword(newPassword)),
                            Param("@ModifiedOn", DateTime.Now),
                            Param("@Id", userId),
                            Param("@OldHash", storedHash)));

                    return rows == 1
                        ? OperationResult.Ok()
                        : OperationResult.Fail("Your password was changed from another session. Please try again.");
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("change password for user " + userId, ex);
            }
        }

        public OperationResult ResetStudentPassword(int studentId, string temporaryPassword)
        {
            const string sql =
                "UPDATE dbo.tbl_User SET [Password] = @Hash, MustChangePassword = 1, ModifiedOn = @ModifiedOn " +
                "WHERE Id = @Id AND UserType = @UserType;";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@Hash", PasswordUtility.HashPassword(temporaryPassword)),
                        Param("@ModifiedOn", DateTime.Now),
                        Param("@Id", studentId),
                        Param("@UserType", UserTypes.Student)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail("This student no longer exists.");
                }
            }
            catch (SqlException ex)
            {
                // Only the student's id is logged; the temporary password never reaches the log.
                return FailureResult("reset password of student " + studentId, ex);
            }
        }

        public User Authenticate(string email, string password)
        {
            const string sql =
                "SELECT Id, Email, [Password], UserType, FirstName, LastName, MobileNo, MustChangePassword " +
                "FROM dbo.tbl_User WHERE Email = @Email;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@Email", email)));
                    if (table.Rows.Count == 0)
                        return null;

                    DataRow row = table.Rows[0];
                    string storedHash = Convert.ToString(row["Password"]);
                    if (!IsPasswordValid(password, storedHash, email))
                        return null;

                    UpgradeHashIfNeeded(db, Convert.ToInt32(row["Id"]), password, storedHash);
                    return MapUser(row);
                }
            }
            catch (SqlException ex)
            {
                throw Failure("authenticate user", ex);
            }
        }

        public bool EmailExists(string email, int excludeUserId)
        {
            const string sql = "SELECT COUNT(1) FROM dbo.tbl_User WHERE Email = @Email AND Id <> @ExcludeId;";

            try
            {
                using (var db = new DBAccess())
                {
                    int count = db.lfnExecuteScaler<int>(sql, Params(
                        Param("@Email", email),
                        Param("@ExcludeId", excludeUserId)));

                    return count > 0;
                }
            }
            catch (SqlException ex)
            {
                throw Failure("check e-mail address", ex);
            }
        }

        public List<User> GetUsersByType(string userType)
        {
            const string sql =
                "SELECT Id, Email, UserType, FirstName, LastName, MobileNo FROM dbo.tbl_User " +
                "WHERE UserType = @UserType ORDER BY FirstName, LastName;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@UserType", userType)));

                    var users = new List<User>();
                    foreach (DataRow row in table.Rows)
                        users.Add(MapUser(row));

                    return users;
                }
            }
            catch (SqlException ex)
            {
                throw Failure("list " + userType + " accounts", ex);
            }
        }

        public User GetUserById(int id)
        {
            const string sql =
                "SELECT Id, Email, UserType, FirstName, LastName, MobileNo FROM dbo.tbl_User WHERE Id = @Id;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@Id", id)));
                    return table.Rows.Count == 0 ? null : MapUser(table.Rows[0]);
                }
            }
            catch (SqlException ex)
            {
                throw Failure("load user " + id, ex);
            }
        }

        public User GetUserByEmail(string email)
        {
            const string sql =
                "SELECT Id, Email, UserType, FirstName, LastName, MobileNo FROM dbo.tbl_User WHERE Email = @Email;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@Email", email)));
                    return table.Rows.Count == 0 ? null : MapUser(table.Rows[0]);
                }
            }
            catch (SqlException ex)
            {
                throw Failure("look up user by e-mail", ex);
            }
        }

        public OperationResult DeleteStudent(int id)
        {
            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();
                    if (!TryLockResource(db, AllocationsLock))
                        return OperationResult.Fail(BusyMessage);

                    int students = db.lfnExecuteScaler<int>(
                        "SELECT COUNT(1) FROM dbo.tbl_User WHERE Id = @Id AND UserType = @UserType;",
                        Params(Param("@Id", id), Param("@UserType", UserTypes.Student)));
                    if (students == 0)
                        return OperationResult.Fail("This student no longer exists.");

                    // Release the room this student holds, otherwise it would stay unavailable forever.
                    db.lfnExecuteNonQuery(
                        "UPDATE R SET R.Availability = 1, R.ModifiedOn = @Now " +
                        "FROM dbo.tbl_Room R INNER JOIN dbo.tbl_RequestRoom RR ON RR.ApprovedRoomId = R.Id " +
                        "WHERE RR.UserId = @Id AND RR.Status = @Approved;",
                        Params(Param("@Now", DateTime.Now), Param("@Id", id), Param("@Approved", RequestStatus.Approved)));

                    // Requests reference the student, so they must go before the account does.
                    db.lfnExecuteNonQuery("DELETE FROM dbo.tbl_RequestRoom WHERE UserId = @Id;", Params(Param("@Id", id)));
                    db.lfnExecuteNonQuery(
                        "DELETE FROM dbo.tbl_User WHERE Id = @Id AND UserType = @UserType;",
                        Params(Param("@Id", id), Param("@UserType", UserTypes.Student)));

                    db.CommitTransaction();
                    return OperationResult.Ok();
                }
            }
            catch (SqlException ex)
            {
                // Leaving the using block rolls the transaction back.
                return FailureResult("delete student " + id, ex);
            }
        }

        public OperationResult DeleteAdmin(int id, int currentAdminId)
        {
            if (id == currentAdminId)
                return OperationResult.Fail("You cannot delete the account you are signed in with.");

            // One statement checks "not the last administrator" and deletes. Administrator deletions queue on a lock
            // first: without it, two administrators deleting each other at the same moment each locked their own row
            // and then needed the other's to count, and SQL Server ended one of them as a deadlock victim.
            const string sql =
                "DELETE FROM dbo.tbl_User WHERE Id = @Id AND UserType = @Admin " +
                "AND (SELECT COUNT(1) FROM dbo.tbl_User WHERE UserType = @Admin) > 1;";

            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();
                    if (!TryLockResource(db, AdministratorsLock))
                        return OperationResult.Fail(BusyMessage);

                    int rows = db.lfnExecuteNonQuery(sql, Params(Param("@Id", id), Param("@Admin", UserTypes.Admin)));
                    db.CommitTransaction();

                    return rows == 1
                        ? OperationResult.Ok()
                        : OperationResult.Fail("The administrator was not deleted: the account no longer exists, or it is the last remaining administrator.");
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("delete administrator " + id, ex);
            }
        }

        /// <summary>
        /// Replaces a legacy or low-cost hash with a current one right after the user proved they know the password,
        /// which is the only moment the plain password is available. Best effort: the sign-in already succeeded and
        /// the old hash stays valid, so a failure is logged and retried at the next sign-in.
        /// </summary>
        private void UpgradeHashIfNeeded(DBAccess db, int userId, string password, string storedHash)
        {
            if (!PasswordUtility.NeedsRehash(storedHash))
                return;

            try
            {
                // Matching on the old hash means a password changed in another session is never overwritten.
                db.lfnExecuteNonQuery(
                    "UPDATE dbo.tbl_User SET [Password] = @NewHash WHERE Id = @Id AND [Password] = @OldHash;",
                    Params(
                        Param("@NewHash", PasswordUtility.HashPassword(password)),
                        Param("@Id", userId),
                        Param("@OldHash", storedHash)));
            }
            catch (SqlException ex)
            {
                // Most likely the Password column is still narrower than the new hash: run Database/upgrade-existing-db.sql.
                Logger.Error("Could not upgrade the password hash of user " + userId + "; the old hash keeps working.", ex);
            }
        }

        private bool IsPasswordValid(string password, string storedHash, string accountLabel)
        {
            try
            {
                return PasswordUtility.VerifyPassword(password, storedHash);
            }
            catch (FormatException ex)
            {
                // A stored hash that is not Base64 means the row was damaged or written by something else.
                Logger.Error("The stored password hash for '" + accountLabel + "' is malformed; this account cannot use its password.", ex);
                return false;
            }
        }

        private static User MapUser(DataRow row)
        {
            string firstName = Convert.ToString(row["FirstName"]);
            string lastName = Convert.ToString(row["LastName"]);

            return new User
            {
                Id = Convert.ToInt32(row["Id"]),
                Email = Convert.ToString(row["Email"]),
                UserType = Convert.ToString(row["UserType"]),
                FirstName = firstName,
                LastName = lastName,
                MobileNo = Convert.ToString(row["MobileNo"]),
                // Only the sign-in query selects the flag.
                MustChangePassword = row.Table.Columns.Contains("MustChangePassword")
                    && row["MustChangePassword"] != DBNull.Value
                    && Convert.ToBoolean(row["MustChangePassword"]),
                UserName = firstName + " " + lastName
            };
        }
    }
}
