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

        public User Authenticate(string email, string password)
        {
            const string sql =
                "SELECT Id, Email, [Password], UserType, FirstName, LastName, MobileNo FROM dbo.tbl_User WHERE Email = @Email;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@Email", email)));
                    if (table.Rows.Count == 0)
                        return null;

                    DataRow row = table.Rows[0];
                    if (!IsPasswordValid(password, Convert.ToString(row["Password"]), email))
                        return null;

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

        public OperationResult DeleteStudent(int id)
        {
            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();

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

            // One statement checks "not the last administrator" and deletes, so the rule cannot be bypassed by two
            // administrators deleting each other at the same time (UPDLOCK/HOLDLOCK serialise the count).
            const string sql =
                "DELETE FROM dbo.tbl_User WHERE Id = @Id AND UserType = @Admin " +
                "AND (SELECT COUNT(1) FROM dbo.tbl_User WITH (UPDLOCK, HOLDLOCK) WHERE UserType = @Admin) > 1;";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(Param("@Id", id), Param("@Admin", UserTypes.Admin)));

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

        private bool IsPasswordValid(string password, string storedHash, string email)
        {
            try
            {
                return PasswordUtility.VerifyPassword(password, storedHash);
            }
            catch (FormatException ex)
            {
                // A stored hash that is not Base64 means the row was damaged or written by something else.
                Logger.Error("The stored password hash for '" + email + "' is malformed; this account cannot sign in.", ex);
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
                UserName = firstName + " " + lastName
            };
        }
    }
}
