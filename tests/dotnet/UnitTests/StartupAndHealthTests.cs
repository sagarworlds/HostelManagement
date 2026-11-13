using System;
using System.Linq;
using Xunit;

namespace HostelManagement.UnitTests
{
    /// <summary>A verifier that says whatever the test needs it to say.</summary>
    internal sealed class FakeSchemaVerifier : ISchemaVerifier
    {
        public string[] Problems = new string[0];
        public bool Unreachable;

        public SchemaCheckResult Verify()
        {
            if (Unreachable)
                throw new DataAccessException("simulated outage", new Exception());

            return new SchemaCheckResult(Problems);
        }
    }

    /// <summary>What the application writes to its log when it starts, and what the health page answers.</summary>
    public class StartupAndHealthTests
    {
        private readonly FakeLogger _log = new FakeLogger();
        private readonly FakeSchemaVerifier _schema = new FakeSchemaVerifier();

        // ---- start-up ----

        [Fact]
        public void A_current_database_is_noted_and_nothing_is_complained_about()
        {
            new StartupChecks(_log, _schema, null).Run();

            Assert.Single(_log.Infos);
            Assert.Empty(_log.Warnings);
            Assert.Empty(_log.Errors);
        }

        [Fact]
        public void A_database_that_lacks_something_is_an_error_naming_it_and_the_upgrade_script()
        {
            _schema.Problems = new[] { "Column dbo.tbl_User.MustChangePassword is missing.", "Table dbo.tbl_Announcement is missing." };

            new StartupChecks(_log, _schema, null).Run();

            string error = Assert.Single(_log.Errors);
            Assert.Contains("MustChangePassword", error);
            Assert.Contains("tbl_Announcement", error);
            Assert.Contains("Database/upgrade-existing-db.sql", error);
            Assert.Empty(_log.Infos);
        }

        [Fact]
        public void A_database_that_cannot_be_read_is_an_error_and_never_stops_the_application_starting()
        {
            _schema.Unreachable = true;

            new StartupChecks(_log, _schema, null).Run();   // does not throw

            Assert.Contains("could not be read", Assert.Single(_log.Errors));
        }

        [Fact]
        public void A_problem_found_in_the_settings_is_reported_even_when_the_database_is_fine()
        {
            new StartupChecks(_log, _schema, "LogRetentionDays is 'abc'.").Run();

            Assert.Contains("LogRetentionDays is 'abc'.", Assert.Single(_log.Warnings));
            Assert.Single(_log.Infos);
        }

        // ---- the health page ----

        [Fact]
        public void The_site_is_healthy_when_the_database_is_reachable_and_complete()
        {
            Assert.True(new HealthCheck(_schema, _log).IsHealthy());
            Assert.Empty(_log.Warnings.Concat(_log.Errors));
        }

        [Fact]
        public void The_site_is_not_healthy_when_something_is_missing_and_the_log_says_what()
        {
            _schema.Problems = new[] { "Table dbo.tbl_Room is missing." };

            Assert.False(new HealthCheck(_schema, _log).IsHealthy());
            Assert.Contains("tbl_Room", Assert.Single(_log.Warnings));
        }

        [Fact]
        public void The_site_is_not_healthy_when_the_database_cannot_be_read_and_the_check_itself_does_not_fail()
        {
            _schema.Unreachable = true;

            Assert.False(new HealthCheck(_schema, _log).IsHealthy());
        }
    }
}
