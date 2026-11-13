namespace HostelManagement
{
    /// <summary>
    /// Checks that the database has the tables and columns this version of the application needs. Start-up and the health
    /// page depend on this interface, not on SQL Server, so they are tested with a fake.
    /// </summary>
    public interface ISchemaVerifier
    {
        /// <summary>Compares the database with <see cref="ExpectedSchema"/>.</summary>
        /// <returns>The result; <see cref="SchemaCheckResult.IsCurrent"/> is false when something is missing.</returns>
        /// <exception cref="DataAccessException">The database could not be read at all (down, refused, not configured); the cause is in the log.</exception>
        SchemaCheckResult Verify();
    }
}
