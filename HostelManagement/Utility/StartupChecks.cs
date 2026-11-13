namespace HostelManagement
{
    /// <summary>
    /// What the application looks at once when it starts, and writes to the log: a deployment that went wrong (a database
    /// that was not upgraded, a setting that is not understood) shows up in the first lines of the log instead of as a
    /// puzzling error on some page later. It never stops the application from starting.
    /// </summary>
    public sealed class StartupChecks
    {
        private readonly ILogger _logger;
        private readonly ISchemaVerifier _schema;
        private readonly string _settingsProblem;

        /// <param name="logger">Where the findings go.</param>
        /// <param name="schema">Checks the database against what this version needs.</param>
        /// <param name="settingsProblem">A problem already found in the configuration, or null; reported first.</param>
        public StartupChecks(ILogger logger, ISchemaVerifier schema, string settingsProblem)
        {
            _logger = logger;
            _schema = schema;
            _settingsProblem = settingsProblem;
        }

        /// <summary>Runs every check and logs what it found. A failing check is logged; it is never thrown.</summary>
        public void Run()
        {
            if (_settingsProblem != null)
                _logger.Warn("Startup: " + _settingsProblem);

            try
            {
                SchemaCheckResult result = _schema.Verify();
                if (result.IsCurrent)
                {
                    _logger.Info("Startup: the database has every table and column this version of the application needs.");
                    return;
                }

                _logger.Error("Startup: the database does not match this version of the application. " + string.Join(" ", result.Problems)
                    + " Run Database/upgrade-existing-db.sql on the database before using the site.", null);
            }
            catch (DataAccessException)
            {
                // The verifier has already logged why (with the exception); say what it means.
                _logger.Error("Startup: the database could not be read, so its schema was not checked. The site will not work until it can be.", null);
            }
        }
    }
}
