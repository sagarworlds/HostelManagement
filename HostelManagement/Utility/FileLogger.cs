using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Web.Hosting;

namespace HostelManagement
{
    /// <summary>
    /// Appends log entries to a daily file under App_Data/Logs. App_Data is never served by IIS, so the
    /// files are not downloadable. Logging must never take the application down: if the file cannot be
    /// written the entry goes to System.Diagnostics.Trace instead. Files older than the "LogRetentionDays"
    /// setting are deleted, once a day (see <see cref="LogRetention"/>).
    /// </summary>
    public class FileLogger : ILogger
    {
        // Several requests can log at once; serialise access so entries are not interleaved or lost.
        private static readonly object WriteLock = new object();

        private readonly string _directory;
        private readonly string _fileNamePrefix;
        private readonly int _keepDays;
        private readonly Func<DateTime> _clock;

        /// <summary>
        /// Logs to App_Data/Logs using the "LogFileName" app setting as the file name prefix, and deletes daily files
        /// older than the "LogRetentionDays" setting.
        /// </summary>
        public FileLogger() : this(ResolveDefaultDirectory(), Config.LogFileName, Config.LogRetentionDays, () => DateTime.Now)
        {
        }

        /// <summary>Logs to the given folder and keeps every file.</summary>
        /// <param name="directory">Folder for the log files; created on first write.</param>
        /// <param name="fileNamePrefix">Prefix of the daily file name, e.g. "log1" gives log1-20261029.log.</param>
        public FileLogger(string directory, string fileNamePrefix) : this(directory, fileNamePrefix, 0, () => DateTime.Now)
        {
        }

        /// <param name="directory">Folder for the log files; created on first write.</param>
        /// <param name="fileNamePrefix">Prefix of the daily file name, e.g. "log1" gives log1-20261029.log.</param>
        /// <param name="keepDays">Days of files to keep; 0 keeps every file.</param>
        /// <param name="clock">The current local time; a test supplies its own.</param>
        public FileLogger(string directory, string fileNamePrefix, int keepDays, Func<DateTime> clock)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A log directory is required.", "directory");
            if (string.IsNullOrWhiteSpace(fileNamePrefix))
                throw new ArgumentException("A log file name prefix is required.", "fileNamePrefix");

            if (keepDays < 0)
                throw new ArgumentOutOfRangeException("keepDays", "Days to keep cannot be negative (0 keeps every file).");
            if (clock == null)
                throw new ArgumentNullException("clock");

            _directory = directory;
            _fileNamePrefix = fileNamePrefix;
            _keepDays = keepDays;
            _clock = clock;
        }

        public void Info(string message)
        {
            if (IsVerboseLoggingEnabled())
                Write("INFO", message, null);
        }

        public void Warn(string message)
        {
            if (IsVerboseLoggingEnabled())
                Write("WARN", message, null);
        }

        // Errors are always written; the "Logging" app setting only silences routine Info/Warn noise.
        public void Error(string message, Exception ex)
        {
            Write("ERROR", message, ex);
        }

        private static bool IsVerboseLoggingEnabled()
        {
            return Config.Logging != "0";
        }

        private static string ResolveDefaultDirectory()
        {
            // MapPath on HostingEnvironment also works outside a request (background threads, app start).
            string appData = HostingEnvironment.MapPath("~/App_Data")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            return Path.Combine(appData, "Logs");
        }

        private void Write(string level, string message, Exception ex)
        {
            DateTime now = _clock();
            string entry = Format(now, level, message, ex);

            try
            {
                lock (WriteLock)
                {
                    Directory.CreateDirectory(_directory);
                    File.AppendAllText(PathOfDailyFile(now), entry, Encoding.UTF8);
                    DeleteExpiredFiles(now);
                }
            }
            catch (IOException ioEx)
            {
                WriteToTrace(ioEx, entry);
            }
            catch (UnauthorizedAccessException accessEx)
            {
                WriteToTrace(accessEx, entry);
            }
            catch (SecurityException securityEx)
            {
                WriteToTrace(securityEx, entry);
            }
        }

        private static string Format(DateTime now, string level, string message, Exception ex)
        {
            var entry = new StringBuilder();
            entry.Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            entry.Append(" [").Append(level).Append("] ").Append(message);
            if (ex != null)
                entry.AppendLine().Append(ex);
            entry.AppendLine();
            return entry.ToString();
        }

        private string PathOfDailyFile(DateTime now)
        {
            return Path.Combine(_directory, _fileNamePrefix + "-" + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
        }

        // Runs after the entry is safely on disk and inside the write lock, so a clean-up can never lose an entry or
        // overlap another write. The first entry of each day does the work; see LogRetention.
        private void DeleteExpiredFiles(DateTime now)
        {
            int deleted = LogRetention.PurgeIfDue(_directory, _fileNamePrefix, now, _keepDays);
            if (deleted > 0 && IsVerboseLoggingEnabled())
                File.AppendAllText(PathOfDailyFile(now), Format(now, "INFO", "Deleted " + deleted + " log file(s) older than " + _keepDays + " days.", null), Encoding.UTF8);
        }

        private static void WriteToTrace(Exception loggingFailure, string entry)
        {
            Trace.TraceError("HostelHub could not write its log file (" + loggingFailure.Message + "). Entry: " + entry);
        }
    }
}
