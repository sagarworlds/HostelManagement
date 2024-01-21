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

        public static string MCQBConnectionString
        {
            get
            {
                return System.Configuration.ConfigurationManager.ConnectionStrings["DBConnection"].ToString();
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

        public static string TempContentPath
        {
            get { return ReadConfigKey("tempContentPath", "C:\\Temp"); }
        }

    }
}