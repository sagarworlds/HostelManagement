using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace HostelManagement
{
    /// <summary>SQL Server implementation of <see cref="ISchemaVerifier"/>: reads the column list the database reports.</summary>
    public class SchemaVerifier : RepositoryBase, ISchemaVerifier
    {
        public SchemaVerifier(ILogger logger) : base(logger)
        {
        }

        public SchemaCheckResult Verify()
        {
            // The application's own login sees every table it has been given rights on, which is every table it uses.
            const string sql = "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = N'dbo';";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params());

                    var found = new List<KeyValuePair<string, string>>();
                    foreach (DataRow row in table.Rows)
                        found.Add(new KeyValuePair<string, string>(Convert.ToString(row["TABLE_NAME"]), Convert.ToString(row["COLUMN_NAME"])));

                    return new SchemaCheckResult(ExpectedSchema.Missing(found));
                }
            }
            catch (SqlException ex)
            {
                throw Failure("check the database schema", ex);
            }
            catch (ConfigurationErrorsException ex)
            {
                // No connection string is as much "the database cannot be read" as a server that is down.
                Logger.Error("Database operation failed: check the database schema (the connection string is not configured)", ex);
                throw new DataAccessException("The database could not be reached (the connection string is not configured). See the application log for details.", ex);
            }
        }
    }
}
