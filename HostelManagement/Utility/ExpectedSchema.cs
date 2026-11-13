using System;
using System.Collections.Generic;
using System.Linq;

namespace HostelManagement
{
    /// <summary>
    /// The tables and columns this version of the application needs, exactly as Database/schema.sql creates them. A database
    /// that lacks any of them was not upgraded when the application was (Database/upgrade-existing-db.sql), and the first
    /// page that touches the missing column fails.
    /// </summary>
    /// <remarks>
    /// A copy of the schema's shape is a second place to keep up to date, so the database tests compare it with what
    /// schema.sql really creates, in both directions, and with what the upgrade script produces. Only names are compared:
    /// a different type or length is not noticed here.
    /// </remarks>
    public static class ExpectedSchema
    {
        private static readonly KeyValuePair<string, string[]>[] Tables =
        {
            Table("tbl_User", "Id", "Email", "Password", "UserType", "FirstName", "LastName", "MobileNo", "MustChangePassword", "CreatedOn", "ModifiedOn"),
            Table("tbl_Room", "Id", "RoomName", "Rent", "Description", "Availability", "ModifiedBy", "CreatedOn", "ModifiedOn"),
            Table("tbl_RequestRoom", "Id", "UserId", "ApprovedRoomId", "PreferredRoomId", "Status", "CreatedOn", "ModifiedOn", "ModifiedBy", "RequestDate", "ResponseDate"),
            Table("tbl_PasswordReset", "Id", "UserId", "TokenHash", "CreatedOn", "ExpiresOn", "UsedOn"),
            Table("tbl_Announcement", "Id", "Title", "Body", "CreatedBy", "CreatedOn", "ExpiresOn"),
        };

        /// <summary>Every expected table in schema <c>dbo</c>, with its columns.</summary>
        public static IEnumerable<KeyValuePair<string, string[]>> TablesAndColumns
        {
            get { return Tables; }
        }

        /// <summary>
        /// What is missing from the database, one sentence per table or column, in a fixed order. Extra tables and columns
        /// are fine: a database may hold more than the application needs.
        /// </summary>
        /// <param name="found">The (table, column) pairs the database has in schema <c>dbo</c>. Names are compared without regard to case.</param>
        public static List<string> Missing(IEnumerable<KeyValuePair<string, string>> found)
        {
            var present = new HashSet<string>(
                found.Select(pair => Key(pair.Key, pair.Value)), StringComparer.OrdinalIgnoreCase);
            var tablesPresent = new HashSet<string>(found.Select(pair => pair.Key), StringComparer.OrdinalIgnoreCase);

            var problems = new List<string>();
            foreach (KeyValuePair<string, string[]> table in Tables)
            {
                if (!tablesPresent.Contains(table.Key))
                {
                    // A login with no rights on a table sees nothing of it, so "missing" may really be "not allowed": say both,
                    // or the administrator runs the upgrade script when what is wrong is the login (Database/create-app-login.sql).
                    problems.Add("Table dbo." + table.Key + " is missing, or the application's database login has no rights on it.");
                    continue;   // its columns are missing too; one line says it
                }

                foreach (string column in table.Value.Where(column => !present.Contains(Key(table.Key, column))))
                    problems.Add("Column dbo." + table.Key + "." + column + " is missing.");
            }

            return problems;
        }

        private static KeyValuePair<string, string[]> Table(string name, params string[] columns)
        {
            return new KeyValuePair<string, string[]>(name, columns);
        }

        private static string Key(string table, string column)
        {
            return table + "." + column;
        }
    }
}
