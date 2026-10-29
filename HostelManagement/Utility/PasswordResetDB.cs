using System;
using System.Data;
using System.Data.SqlClient;

namespace HostelManagement
{
    /// <summary>SQL Server implementation of <see cref="IPasswordResetRepository"/>.</summary>
    public class PasswordResetDB : RepositoryBase, IPasswordResetRepository
    {
        public PasswordResetDB(ILogger logger) : base(logger)
        {
        }

        public OperationResult CreateToken(int userId, string tokenHash, DateTime createdOn, DateTime expiresOn)
        {
            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();

                    // Only the newest e-mail may work: an older link that is still in someone's inbox is retired.
                    db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_PasswordReset SET UsedOn = @Now WHERE UserId = @UserId AND UsedOn IS NULL;",
                        Params(Param("@Now", createdOn), Param("@UserId", userId)));

                    db.lfnExecuteNonQuery(
                        "INSERT INTO dbo.tbl_PasswordReset (UserId, TokenHash, CreatedOn, ExpiresOn) " +
                        "VALUES (@UserId, @TokenHash, @CreatedOn, @ExpiresOn);",
                        Params(
                            Param("@UserId", userId),
                            Param("@TokenHash", tokenHash),
                            Param("@CreatedOn", createdOn),
                            Param("@ExpiresOn", expiresOn)));

                    db.CommitTransaction();
                    return OperationResult.Ok();
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("create password reset token for user " + userId, ex);
            }
        }

        public bool IsTokenValid(string tokenHash, DateTime now)
        {
            const string sql =
                "SELECT COUNT(1) FROM dbo.tbl_PasswordReset WHERE TokenHash = @TokenHash AND UsedOn IS NULL AND ExpiresOn > @Now;";

            try
            {
                using (var db = new DBAccess())
                {
                    int count = db.lfnExecuteScaler<int>(sql, Params(Param("@TokenHash", tokenHash), Param("@Now", now)));
                    return count > 0;
                }
            }
            catch (SqlException ex)
            {
                throw Failure("check password reset token", ex);
            }
        }

        public OperationResult CompleteReset(string tokenHash, string newPasswordHash, DateTime now)
        {
            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();

                    // UPDLOCK makes a second use of the same link wait for the first to finish and then find it used.
                    DataTable token = db.lfnGetDataTable(
                        "SELECT UserId FROM dbo.tbl_PasswordReset WITH (UPDLOCK, ROWLOCK) " +
                        "WHERE TokenHash = @TokenHash AND UsedOn IS NULL AND ExpiresOn > @Now;",
                        Params(Param("@TokenHash", tokenHash), Param("@Now", now)));
                    if (token.Rows.Count == 0)
                        return OperationResult.Fail(PasswordResetService.InvalidLinkMessage);

                    int userId = Convert.ToInt32(token.Rows[0]["UserId"]);

                    int updated = db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_User SET [Password] = @NewHash, MustChangePassword = 0, ModifiedOn = @Now WHERE Id = @UserId;",
                        Params(Param("@NewHash", newPasswordHash), Param("@Now", now), Param("@UserId", userId)));
                    if (updated != 1)
                        return OperationResult.Fail("This account no longer exists.");

                    // This link and any other unused link of the same user are spent.
                    db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_PasswordReset SET UsedOn = @Now WHERE UserId = @UserId AND UsedOn IS NULL;",
                        Params(Param("@Now", now), Param("@UserId", userId)));

                    db.CommitTransaction();
                    return OperationResult.Ok();
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("complete password reset", ex);
            }
        }
    }
}
