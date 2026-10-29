using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;

namespace HostelManagement
{
    /// <summary>
    /// Plumbing shared by the repositories: parameter building, logging of database failures and
    /// classification of SQL Server errors. Business SQL stays in the concrete repositories.
    /// </summary>
    public abstract class RepositoryBase
    {
        // SQL Server error numbers.
        private const int UniqueConstraintViolation = 2627;
        private const int UniqueIndexViolation = 2601;
        private const int ForeignKeyViolation = 547;
        private const int InvalidColumnName = 207;
        private const int InvalidObjectName = 208;

        /// <summary>Shown for write failures the user cannot fix; details are in the log.</summary>
        protected const string GenericWriteFailureMessage =
            "Something went wrong while saving. Please try again, and contact the administrator if it keeps happening.";

        /// <summary>Shown when a write gave up waiting for another administrator's change to finish.</summary>
        protected const string BusyMessage = "Another change is being saved right now. Please try again in a moment.";

        // Names for TryLockResource. Every transaction that must not overlap with another one takes the same name first.
        /// <summary>Held by anything that changes which student holds which room, or removes a room.</summary>
        protected const string AllocationsLock = "HostelHub.Allocations";

        /// <summary>Held while an administrator account is deleted, so the "never the last one" rule cannot be raced.</summary>
        protected const string AdministratorsLock = "HostelHub.Administrators";

        private const int LockWaitMilliseconds = 10000;

        protected RepositoryBase(ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException("logger");

            Logger = logger;
        }

        protected ILogger Logger { get; private set; }

        /// <summary>
        /// Takes an exclusive lock on a name rather than on table rows, held until the open transaction ends. Call it
        /// straight after <c>BeginTransaction</c>, before touching any row.
        /// </summary>
        /// <remarks>
        /// Two transactions that each lock some rows and then need the other's rows deadlock, and SQL Server ends one of
        /// them with an error (found by running two administrators at once). Queueing on one name first means they run
        /// one after the other and each sees the other's finished result, so the usual checks ("is the room still
        /// free?", "is this the last administrator?") give the right answer.
        /// </remarks>
        /// <returns>False when the lock was not obtained within ten seconds; the caller should return <see cref="BusyMessage"/>.</returns>
        protected bool TryLockResource(DBAccess db, string resource)
        {
            // sp_getapplock returns 0 or 1 when the lock is granted and a negative number otherwise (-1 timed out).
            int code = db.lfnExecuteScaler<int>(
                "DECLARE @Result INT; " +
                "EXEC @Result = sys.sp_getapplock @Resource = @Name, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = @WaitMs; " +
                "SELECT @Result;",
                Params(Param("@Name", resource), Param("@WaitMs", LockWaitMilliseconds)));

            if (code >= 0)
                return true;

            Logger.Warn("Gave up waiting for the lock '" + resource + "' (sp_getapplock returned " + code + ").");
            return false;
        }

        /// <summary>Builds a parameter; a null value is sent as SQL NULL (a raw null would mean "not supplied").</summary>
        protected static SqlParameter Param(string name, object value)
        {
            return new SqlParameter { ParameterName = name, Value = value ?? DBNull.Value };
        }

        /// <summary>Same as <see cref="Param(string, object)"/> but with an explicit SQL type, for nullable columns where inference could guess wrong.</summary>
        protected static SqlParameter Param(string name, SqlDbType type, object value)
        {
            return new SqlParameter(name, type) { Value = value ?? DBNull.Value };
        }

        protected static ArrayList Params(params SqlParameter[] parameters)
        {
            return new ArrayList(parameters);
        }

        /// <summary>
        /// Logs a failed read and returns the exception to throw. Callers write
        /// <c>throw Failure("load rooms", ex);</c>.
        /// </summary>
        protected DataAccessException Failure(string operation, SqlException ex)
        {
            Logger.Error("Database operation failed: " + operation + SchemaHint(ex), ex);
            return new DataAccessException(
                "The database could not complete the request (" + operation + "). See the application log for details.", ex);
        }

        /// <summary>Logs a failed write and returns the result to hand back to the caller.</summary>
        protected OperationResult FailureResult(string operation, SqlException ex)
        {
            Logger.Error("Database operation failed: " + operation + SchemaHint(ex), ex);
            return OperationResult.Fail(GenericWriteFailureMessage);
        }

        protected static bool IsUniqueViolation(SqlException ex)
        {
            return HasError(ex, UniqueConstraintViolation) || HasError(ex, UniqueIndexViolation);
        }

        protected static bool IsForeignKeyViolation(SqlException ex)
        {
            return HasError(ex, ForeignKeyViolation);
        }

        /// <summary>
        /// A missing column or table almost always means the code was deployed before the database upgrade;
        /// say so in the log instead of leaving a bare "Invalid column name".
        /// </summary>
        private static string SchemaHint(SqlException ex)
        {
            return HasError(ex, InvalidColumnName) || HasError(ex, InvalidObjectName)
                ? " (the database schema looks out of date: run Database/upgrade-existing-db.sql)"
                : string.Empty;
        }

        private static bool HasError(SqlException ex, int number)
        {
            foreach (SqlError error in ex.Errors)
            {
                if (error.Number == number)
                    return true;
            }

            return false;
        }
    }
}
