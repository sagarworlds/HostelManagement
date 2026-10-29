using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// Runs the .sql files from the Database folder the way SSMS or sqlcmd would: split into batches at lines holding
    /// only GO (which is not T-SQL, so the server cannot take it), each batch executed in turn on one connection.
    /// </summary>
    public static class SqlScript
    {
        private static readonly Regex BatchSeparator = new Regex(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        /// <summary>The scripts name their database HMSDB; tests run against a disposable copy under another name.</summary>
        private static readonly Regex ProductionName = new Regex(@"\bHMSDB\b");

        public static IEnumerable<string> Batches(string script)
        {
            foreach (string batch in BatchSeparator.Split(script))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                    yield return batch;
            }
        }

        /// <summary>Points a script at the disposable test database instead of HMSDB.</summary>
        public static string Retarget(string script, string databaseName)
        {
            return ProductionName.Replace(script, databaseName);
        }

        /// <summary>
        /// Runs every batch and stops at the first error (sqlcmd without -b would carry on, which hides mistakes).
        /// </summary>
        /// <returns>Every result set the script produced, in order (the upgrade script ends with diagnostics).</returns>
        public static List<DataTable> Run(string connectionString, string script)
        {
            var resultSets = new List<DataTable>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                foreach (string batch in Batches(script))
                {
                    using (var command = new SqlCommand(batch, connection) { CommandTimeout = 120 })
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var data = new DataSet();
                        adapter.Fill(data);
                        foreach (DataTable table in data.Tables)
                            resultSets.Add(table);
                    }
                }
            }

            return resultSets;
        }
    }
}
