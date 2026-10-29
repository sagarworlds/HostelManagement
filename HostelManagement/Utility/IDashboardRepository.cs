namespace HostelManagement
{
    /// <summary>Read-only statistics for the administrator dashboard.</summary>
    public interface IDashboardRepository
    {
        /// <summary>Counts rooms, requests and accounts in one round trip.</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        DashboardSummary GetSummary();
    }
}
