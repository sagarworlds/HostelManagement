using System;
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

        /// <summary>
        /// Sets the text value of one "DECLARE @Name type = N'...';" line, the way a person edits a script's settings
        /// before running it. Insists on exactly one such line, so a script that changed shape cannot be half-edited.
        /// </summary>
        public static string WithDeclaredValue(string script, string variable, string value)
        {
            var declaration = new Regex(@"(DECLARE\s+@" + Regex.Escape(variable) + @"\s+[A-Za-z]+(?:\(\d+\))?\s*=\s*N')[^']*(')");
            if (declaration.Matches(script).Count != 1)
                throw new InvalidOperationException("Expected exactly one DECLARE @" + variable + " = N'...' line in the script.");

            // A quote inside the value is doubled, as T-SQL requires inside a string literal.
            return declaration.Replace(script, match => match.Groups[1].Value + value.Replace("'", "''") + match.Groups[2].Value);
        }

        /// <summary>
        /// Sets one on/off switch, a "DECLARE @Name BIT = 0;" line, the way a person edits a script's settings before
        /// running it. Insists on exactly one such line, like <see cref="WithDeclaredValue"/>.
        /// </summary>
        public static string WithDeclaredFlag(string script, string variable, bool value)
        {
            var declaration = new Regex(@"(DECLARE\s+@" + Regex.Escape(variable) + @"\s+BIT\s*=\s*)[01]\b");
            if (declaration.Matches(script).Count != 1)
                throw new InvalidOperationException("Expected exactly one DECLARE @" + variable + " BIT = 0 or 1 line in the script.");

            return declaration.Replace(script, match => match.Groups[1].Value + (value ? "1" : "0"));
        }

        /// <summary>Points a script at the disposable test database instead of HMSDB.</summary>
        public static string Retarget(string script, string databaseName)
        {
            return ProductionName.Replace(script, databaseName);
        }

        /// <summary>
        /// Runs every batch and stops at the first error (sqlcmd without -b would carry on, which hides mistakes).
        /// </summary>
        /// <param name="connectionString">The server and login to run as.</param>
        /// <param name="script">The script text, GO separators included.</param>
        /// <param name="onMessage">
        /// Receives every PRINT line and informational message the server sends, as it arrives. The backup and restore
        /// scripts report in text, and the lines printed before a failure are the ones that explain it.
        /// </param>
        /// <returns>Every result set the script produced, in order (the upgrade script ends with diagnostics).</returns>
        /// <exception cref="SqlException">The first error a batch raises; later batches are not run.</exception>
        public static List<DataTable> Run(string connectionString, string script, Action<string> onMessage = null)
        {
            var resultSets = new List<DataTable>();

            using (var connection = new SqlConnection(connectionString))
            {
                if (onMessage != null)
                    connection.InfoMessage += (sender, e) => onMessage(e.Message);

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

        /// <summary>
        /// Runs the script the way SSMS does: an error is reported like any other message, and the client keeps reading
        /// whatever the script does next. <see cref="Run"/> cannot show that, because it throws at the first error. This
        /// is how to tell whether a script STOPS after a failed statement (XACT_ABORT), or carries on and, say, announces
        /// success under its own error messages.
        /// </summary>
        public static ScriptOutput RunReportingErrors(string connectionString, string script)
        {
            var output = new ScriptOutput();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.FireInfoMessageEventOnUserErrors = true;
                connection.InfoMessage += (sender, e) =>
                {
                    output.Messages.Add(e.Message);
                    foreach (SqlError error in e.Errors)
                    {
                        if (error.Class > 10)
                            output.Errors.Add(error);
                    }
                };

                connection.Open();
                foreach (string batch in Batches(script))
                {
                    using (var command = new SqlCommand(batch, connection) { CommandTimeout = 120 })
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var data = new DataSet();
                        adapter.Fill(data);
                        foreach (DataTable table in data.Tables)
                            output.ResultSets.Add(table);
                    }
                }
            }

            return output;
        }
    }

    /// <summary>What <see cref="SqlScript.RunReportingErrors"/> saw: everything the script printed, raised and returned, in order.</summary>
    public sealed class ScriptOutput
    {
        /// <summary>Every message, PRINT lines and error texts alike.</summary>
        public List<string> Messages { get; } = new List<string>();

        /// <summary>The errors among them (severity above 10).</summary>
        public List<SqlError> Errors { get; } = new List<SqlError>();

        public List<DataTable> ResultSets { get; } = new List<DataTable>();
    }
}
