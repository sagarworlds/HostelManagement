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
    /// written the entry goes to System.Diagnostics.Trace instead.
    /// </summary>
    public class FileLogger : ILogger
    {
        // Several requests can log at once; serialise access so entries are not interleaved or lost.
        private static readonly object WriteLock = new object();

        private readonly string _directory;
        private readonly string _fileNamePrefix;

        /// <summary>Logs to App_Data/Logs using the "LogFileName" app setting as the file name prefix.</summary>
        public FileLogger() : this(ResolveDefaultDirectory(), Config.LogFileName)
        {
        }

        /// <param name="directory">Folder for the log files; created on first write.</param>
        /// <param name="fileNamePrefix">Prefix of the daily file name, e.g. "log1" gives log1-20261029.log.</param>
        public FileLogger(string directory, string fileNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A log directory is required.", "directory");
            if (string.IsNullOrWhiteSpace(fileNamePrefix))
                throw new ArgumentException("A log file name prefix is required.", "fileNamePrefix");

            _directory = directory;
            _fileNamePrefix = fileNamePrefix;
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
            var entry = new StringBuilder();
            entry.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            entry.Append(" [").Append(level).Append("] ").Append(message);
            if (ex != null)
                entry.AppendLine().Append(ex);
            entry.AppendLine();

            string fileName = _fileNamePrefix + "-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log";

            try
            {
                lock (WriteLock)
                {
                    Directory.CreateDirectory(_directory);
                    File.AppendAllText(Path.Combine(_directory, fileName), entry.ToString(), Encoding.UTF8);
                }
            }
            catch (IOException ioEx)
            {
                WriteToTrace(ioEx, entry.ToString());
            }
            catch (UnauthorizedAccessException accessEx)
            {
                WriteToTrace(accessEx, entry.ToString());
            }
            catch (SecurityException securityEx)
            {
                WriteToTrace(securityEx, entry.ToString());
            }
        }

        private static void WriteToTrace(Exception loggingFailure, string entry)
        {
            Trace.TraceError("HostelHub could not write its log file (" + loggingFailure.Message + "). Entry: " + entry);
        }
    }
}
