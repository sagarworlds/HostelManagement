using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HostelManagement.UnitTests
{
    /// <summary>Which daily log files go, which never do, and the once-a-day clean-up of a real folder.</summary>
    public class LogRetentionTests : IDisposable
    {
        private static readonly DateTime Today = new DateTime(2026, 11, 13, 15, 30, 0);

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "hostelhub-logs-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        private static string[] Expired(string prefix, DateTime today, int keepDays, params string[] names)
        {
            return LogRetention.ExpiredFiles(names, prefix, today, keepDays).ToArray();
        }

        private string Make(string name)
        {
            Directory.CreateDirectory(_folder);
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, "x");
            return path;
        }

        // ---- which files have expired ----

        [Fact]
        public void The_day_that_is_keepDays_back_has_expired_and_the_day_after_it_has_not()
        {
            // Keeping 3 days means today and the two before it.
            string[] expired = Expired("log1", Today, 3,
                "log1-20261108.log", "log1-20261109.log", "log1-20261110.log", "log1-20261111.log", "log1-20261112.log", "log1-20261113.log");

            Assert.Equal(new[] { "log1-20261108.log", "log1-20261109.log", "log1-20261110.log" }, expired);
        }

        [Fact]
        public void The_time_of_day_makes_no_difference()
        {
            string[] names = { "log1-20261110.log", "log1-20261111.log" };

            Assert.Equal(Expired("log1", Today.Date, 3, names), Expired("log1", Today.Date.AddHours(23).AddMinutes(59), 3, names));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Nothing_expires_when_retention_is_switched_off(int keepDays)
        {
            Assert.Empty(Expired("log1", Today, keepDays, "log1-19990101.log"));
        }

        [Fact]
        public void Only_the_loggers_own_daily_files_can_expire()
        {
            string[] expired = Expired("log1", Today, 30,
                "log1-20200101.log",        // the logger's own, and old: goes
                "log2-20200101.log",        // another prefix
                "xlog1-20200101.log",       // prefix only as a suffix of another name
                "log1-20200101.txt",        // not a log
                "log1-20200101.log.bak",    // somebody's copy
                "log1-2020.log",            // not a date
                "log1-20261399.log",        // eight digits, but not a date
                "notes.log");

            Assert.Equal(new[] { "log1-20200101.log" }, expired);
        }

        [Fact]
        public void A_prefix_with_characters_that_mean_something_in_a_pattern_is_taken_literally()
        {
            string[] expired = Expired("app.v1+", Today, 30, "app.v1+-20200101.log", "appxv1+-20200101.log", "app.v11-20200101.log");

            Assert.Equal(new[] { "app.v1+-20200101.log" }, expired);
        }

        // ---- the LogRetentionDays setting ----

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void A_missing_setting_means_the_default_without_complaint(string raw)
        {
            RetentionSetting setting = LogRetention.ParseSetting(raw);

            Assert.Equal(LogRetention.DefaultDays, setting.Days);
            Assert.Null(setting.Problem);
        }

        [Theory]
        [InlineData("30", 30)]
        [InlineData(" 7 ", 7)]
        [InlineData("0", 0)]
        public void A_whole_number_is_taken_as_written_and_zero_keeps_everything(string raw, int days)
        {
            RetentionSetting setting = LogRetention.ParseSetting(raw);

            Assert.Equal(days, setting.Days);
            Assert.Null(setting.Problem);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("-5")]
        [InlineData("1.5")]
        [InlineData("30 days")]
        [InlineData("99999999999")]
        public void Anything_else_keeps_the_default_and_says_what_was_wrong(string raw)
        {
            RetentionSetting setting = LogRetention.ParseSetting(raw);

            Assert.Equal(LogRetention.DefaultDays, setting.Days);
            Assert.Contains("'" + raw + "'", setting.Problem);
            Assert.Contains(LogRetention.DefaultDays.ToString(), setting.Problem);
        }

        // ---- cleaning a folder ----

        [Fact]
        public void The_clean_up_deletes_expired_daily_files_and_nothing_else()
        {
            string old = Make("log1-20200101.log");
            string recent = Make("log1-20261112.log");
            string today = Make("log1-20261113.log");
            string other = Make("notes.txt");
            string otherPrefix = Make("audit-20200101.log");

            int deleted = LogRetention.PurgeIfDue(_folder, "log1", Today, 90);

            Assert.Equal(1, deleted);
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(recent));
            Assert.True(File.Exists(today));
            Assert.True(File.Exists(other));
            Assert.True(File.Exists(otherPrefix));
        }

        [Fact]
        public void The_clean_up_runs_once_a_day_per_folder_and_again_the_next_day()
        {
            Make("log1-20200101.log");
            LogRetention.PurgeIfDue(_folder, "log1", Today, 90);

            string laterThatDay = Make("log1-20200102.log");
            Assert.Equal(0, LogRetention.PurgeIfDue(_folder, "log1", Today.AddHours(3), 90));
            Assert.True(File.Exists(laterThatDay), "a request must not scan the folder every time it logs");

            Assert.Equal(1, LogRetention.PurgeIfDue(_folder, "log1", Today.AddDays(1), 90));
            Assert.False(File.Exists(laterThatDay));
        }

        [Fact]
        public void The_clean_up_deletes_nothing_when_retention_is_switched_off()
        {
            string old = Make("log1-20200101.log");

            Assert.Equal(0, LogRetention.PurgeIfDue(_folder, "log1", Today, 0));

            Assert.True(File.Exists(old));
        }

        [Fact]
        public void A_folder_that_does_not_exist_yet_has_nothing_to_clean()
        {
            Assert.Equal(0, LogRetention.PurgeIfDue(Path.Combine(_folder, "never-made"), "log1", Today, 90));
        }
    }
}
