using System;

namespace HostelManagement
{
    /// <summary>
    /// A read could not be completed because the database failed. Repositories log the underlying
    /// SqlException and throw this instead of returning an empty result, so an outage is never
    /// mistaken for "no data".
    /// </summary>
    public class DataAccessException : Exception
    {
        public DataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
