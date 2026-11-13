using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

namespace HostelManagement
{
    /// <summary>
    /// Keeps the application log from growing for ever: deletes the daily log files that <see cref="FileLogger"/> writes once
    /// they are older than the configured number of days. The log holds e-mail addresses (every sign-in), so it should not be
    /// kept longer than it is useful.
    /// </summary>
    /// <remarks>
    /// Only files named exactly like the logger's own (prefix-yyyyMMdd.log) are ever considered, so a file that somebody put
    /// in the folder is never deleted. Deciding what has expired is a pure function, tested without a disk;
    /// <see cref="PurgeIfDue"/> is the only part that touches files.
    /// </remarks>
    public static class LogRetention
    {
        /// <summary>Days of logs kept when the setting is absent: long enough to look back over a term, short enough not to hoard addresses.</summary>
        public const int DefaultDays = 90;

        // One clean-up per day and log folder is plenty, and a request must not scan the folder every time it logs.
        // FileLogger objects are created per request, so the record of "already done today" has to be shared.
        private static readonly object PurgeLock = new object();
        private static readonly Dictionary<string, DateTime> LastPurgeDay = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The daily log files among <paramref name="fileNames"/> that fall outside the retention period. A file dated
        /// <paramref name="keepDays"/> days before <paramref name="today"/> or earlier has expired, so 90 keeps today and
        /// the 89 days before it.
        /// </summary>
        /// <param name="fileNames">File names, without folders.</param>
        /// <param name="prefix">The logger's file name prefix; a file with any other name is never reported.</param>
        /// <param name="today">The current date; its time of day is ignored.</param>
        /// <param name="keepDays">How many days to keep; 0 (or less) keeps everything.</param>
        public static IEnumerable<string> ExpiredFiles(IEnumerable<string> fileNames, string prefix, DateTime today, int keepDays)
        {
            if (keepDays <= 0)
                yield break;

            var dailyLog = new Regex("^" + Regex.Escape(prefix) + @"-(\d{8})\.log$", RegexOptions.CultureInvariant);
            DateTime lastExpiredDay = today.Date.AddDays(-keepDays);

            foreach (string name in fileNames)
            {
                Match match = dailyLog.Match(name);
                DateTime day;
                // An eight-digit name that is not a date (20261399) is not one of the logger's files either.
                if (match.Success
                    && DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)
                    && day <= lastExpiredDay)
                {
                    yield return name;
                }
            }
        }

        /// <summary>Reads the "LogRetentionDays" app setting. A value that is not a whole number is reported, not guessed at.</summary>
        /// <param name="raw">The setting as written; null or blank means "not set".</param>
        public static RetentionSetting ParseSetting(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new RetentionSetting(DefaultDays, null);

            int days;
            // Digits only: a sign or a decimal point means somebody meant something else.
            if (int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out days))
                return new RetentionSetting(days, null);

            return new RetentionSetting(DefaultDays,
                "The app setting LogRetentionDays is '" + raw + "', which is not a whole number of days (0 keeps every log). Keeping " + DefaultDays + " days.");
        }

        /// <summary>
        /// Deletes the expired log files in <paramref name="directory"/>, at most once per calendar day for that folder and
        /// prefix. A file that cannot be deleted is reported to System.Diagnostics.Trace (the logger itself cannot be used
        /// to report on its own folder) and does not stop the others.
        /// </summary>
        /// <param name="directory">The log folder; a missing folder has nothing to clean.</param>
        /// <param name="prefix">The logger's file name prefix.</param>
        /// <param name="now">The current date and time.</param>
        /// <param name="keepDays">How many days to keep; 0 (or less) turns the clean-up off.</param>
        /// <returns>How many files were deleted; 0 when nothing was due.</returns>
        public static int PurgeIfDue(string directory, string prefix, DateTime now, int keepDays)
        {
            if (keepDays <= 0)
                return 0;

            string key = Path.GetFullPath(directory) + "|" + prefix;
            lock (PurgeLock)
            {
                DateTime lastDay;
                if (LastPurgeDay.TryGetValue(key, out lastDay) && lastDay == now.Date)
                    return 0;

                // Recorded before the attempt: a folder that cannot be cleaned must not be retried on every log entry.
                LastPurgeDay[key] = now.Date;
            }

            if (!Directory.Exists(directory))
                return 0;

            int deleted = 0;
            try
            {
                var names = Directory.GetFiles(directory, prefix + "-*.log").Select(Path.GetFileName).ToList();
                foreach (string expired in ExpiredFiles(names, prefix, now, keepDays))
                {
                    if (TryDelete(Path.Combine(directory, expired)))
                        deleted++;
                }
            }
            catch (IOException ex)
            {
                Trace.TraceError("HostelHub could not list its log folder for clean-up (" + ex.Message + ").");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.TraceError("HostelHub could not list its log folder for clean-up (" + ex.Message + ").");
            }
            catch (SecurityException ex)
            {
                Trace.TraceError("HostelHub could not list its log folder for clean-up (" + ex.Message + ").");
            }

            return deleted;
        }

        private static bool TryDelete(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (IOException ex)
            {
                Trace.TraceError("HostelHub could not delete the old log file " + path + " (" + ex.Message + ").");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.TraceError("HostelHub could not delete the old log file " + path + " (" + ex.Message + ").");
            }

            return false;
        }
    }

    /// <summary>The outcome of reading the "LogRetentionDays" setting.</summary>
    public sealed class RetentionSetting
    {
        public RetentionSetting(int days, string problem)
        {
            Days = days;
            Problem = problem;
        }

        /// <summary>The number of days to keep; 0 keeps everything.</summary>
        public int Days { get; private set; }

        /// <summary>What is wrong with the setting, or null when it is fine. Reported at start-up.</summary>
        public string Problem { get; private set; }
    }
}
