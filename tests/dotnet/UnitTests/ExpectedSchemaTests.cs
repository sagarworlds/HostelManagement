using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HostelManagement.UnitTests
{
    /// <summary>
    /// Comparing the database's columns with what the application needs. (That the list itself matches what schema.sql creates
    /// is a database test: SchemaVerifierTests.)
    /// </summary>
    public class ExpectedSchemaTests
    {
        private static List<KeyValuePair<string, string>> Everything()
        {
            return ExpectedSchema.TablesAndColumns
                .SelectMany(table => table.Value.Select(column => new KeyValuePair<string, string>(table.Key, column)))
                .ToList();
        }

        [Fact]
        public void A_database_that_has_everything_has_no_problems()
        {
            Assert.Empty(ExpectedSchema.Missing(Everything()));
        }

        [Fact]
        public void More_than_the_application_needs_is_fine()
        {
            List<KeyValuePair<string, string>> found = Everything();
            found.Add(new KeyValuePair<string, string>("tbl_User", "Nickname"));
            found.Add(new KeyValuePair<string, string>("tbl_Extra", "Id"));

            Assert.Empty(ExpectedSchema.Missing(found));
        }

        [Fact]
        public void A_missing_column_is_named_with_its_table()
        {
            List<KeyValuePair<string, string>> found = Everything().Where(p => !(p.Key == "tbl_User" && p.Value == "MustChangePassword")).ToList();

            Assert.Equal(new[] { "Column dbo.tbl_User.MustChangePassword is missing." }, ExpectedSchema.Missing(found));
        }

        [Fact]
        public void A_missing_table_is_reported_once_and_not_once_per_column()
        {
            List<KeyValuePair<string, string>> found = Everything().Where(p => p.Key != "tbl_Announcement").ToList();

            string[] problems = ExpectedSchema.Missing(found).ToArray();

            Assert.Single(problems);
            Assert.StartsWith("Table dbo.tbl_Announcement is missing", problems[0]);
            Assert.Contains("no rights on it", problems[0]);   // a login that cannot see a table looks the same as a table that is not there
        }

        [Fact]
        public void A_database_that_looks_empty_is_reported_table_by_table()
        {
            Assert.Equal(ExpectedSchema.TablesAndColumns.Count(), ExpectedSchema.Missing(new KeyValuePair<string, string>[0]).Count);
        }

        [Fact]
        public void Names_are_compared_without_regard_to_case()
        {
            List<KeyValuePair<string, string>> shouting = Everything()
                .Select(p => new KeyValuePair<string, string>(p.Key.ToUpperInvariant(), p.Value.ToUpperInvariant()))
                .ToList();

            Assert.Empty(ExpectedSchema.Missing(shouting));
        }

        [Fact]
        public void Problems_come_in_the_order_of_the_expected_schema()
        {
            List<KeyValuePair<string, string>> found = Everything()
                .Where(p => !(p.Key == "tbl_Room" && p.Value == "Rent") && !(p.Key == "tbl_User" && p.Value == "Email"))
                .ToList();

            Assert.Equal(new[] { "Column dbo.tbl_User.Email is missing.", "Column dbo.tbl_Room.Rent is missing." }, ExpectedSchema.Missing(found));
        }
    }
}
