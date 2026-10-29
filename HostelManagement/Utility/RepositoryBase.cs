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

        protected RepositoryBase(ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException("logger");

            Logger = logger;
        }

        protected ILogger Logger { get; private set; }

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
