namespace HostelManagement
{
    /// <summary>
    /// Answers "can this site do its job right now?" for a monitor: the database must be reachable and complete. The answer
    /// is a plain yes or no; the reason goes to the log, because the page is open to anyone and should not describe the
    /// deployment to a stranger.
    /// </summary>
    public sealed class HealthCheck
    {
        private readonly ISchemaVerifier _schema;
        private readonly ILogger _logger;

        public HealthCheck(ISchemaVerifier schema, ILogger logger)
        {
            _schema = schema;
            _logger = logger;
        }

        /// <summary>Checks the database. Never throws: a failing check is exactly what this reports.</summary>
        /// <returns>True when the site can reach a database that has every table and column it needs.</returns>
        public bool IsHealthy()
        {
            try
            {
                SchemaCheckResult result = _schema.Verify();
                if (!result.IsCurrent)
                    _logger.Warn("Health check failed: the database does not match this version of the application. " + string.Join(" ", result.Problems));

                return result.IsCurrent;
            }
            catch (DataAccessException)
            {
                // The verifier has already logged the cause.
                return false;
            }
        }
    }
}
