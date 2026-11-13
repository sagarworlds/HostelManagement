using System;
using System.Configuration;

namespace HostelManagement
{
    public class Config
    {
        private static string ReadConfigKey(string Key)
        {
            if (ConfigurationManager.AppSettings[Key] == null)
                throw new Exception("Key '" + Key + "' not found in configuration");

            if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[Key]))
                throw new Exception("Key '" + Key + "' not configured, please check configuration. ");

            return ConfigurationManager.AppSettings[Key];
        }

        private static string ReadConfigKey(string Key, string DefaultValue)
        {
            if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[Key]))
                return DefaultValue;

            return ConfigurationManager.AppSettings[Key];
        }

        /// <summary>
        /// Connection string named "DBConnection".
        /// </summary>
        /// <exception cref="ConfigurationErrorsException">The connection string is missing or empty.</exception>
        public static string MCQBConnectionString
        {
            get
            {
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["DBConnection"];
                if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                    throw new ConfigurationErrorsException("Connection string 'DBConnection' is missing or empty. " +
                        "Copy HostelManagement/connectionStrings.config.example to connectionStrings.config and set your SQL Server details.");

                return settings.ConnectionString;
            }
        }


        public static string SQLQueryTimeout
        {
            get { return ReadConfigKey("SQLQueryTimeout", "30"); }
        }

        public bool LogSQLMessages
        {
            get { return Convert.ToBoolean(ReadConfigKey("LogSQLMessages", "true")); }
        }

        public static string Logging
        {
            get { return ReadConfigKey("Logging", "1"); }
        }

        public static string LogFileName
        {
            get { return ReadConfigKey("LogFileName", "log1"); }
        }

        /// <summary>
        /// Days of daily log files to keep ("LogRetentionDays"); 0 keeps every file. 90 when the setting is absent or is
        /// not a whole number (<see cref="LogRetentionProblem"/> says so at start-up).
        /// </summary>
        public static int LogRetentionDays
        {
            get { return LogRetention.ParseSetting(ConfigurationManager.AppSettings["LogRetentionDays"]).Days; }
        }

        /// <summary>What is wrong with the "LogRetentionDays" setting, or null when it is fine.</summary>
        public static string LogRetentionProblem
        {
            get { return LogRetention.ParseSetting(ConfigurationManager.AppSettings["LogRetentionDays"]).Problem; }
        }

        public static string TempContentPath
        {
            get { return ReadConfigKey("tempContentPath", "C:\\Temp"); }
        }

    }
}