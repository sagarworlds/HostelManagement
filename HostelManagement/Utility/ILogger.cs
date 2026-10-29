using System;

namespace HostelManagement
{
    /// <summary>
    /// Application logging abstraction. Data-access and page code depend on this interface rather than
    /// on a concrete log destination, so the destination can change (or be faked in tests) without
    /// touching callers.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Records a routine event, e.g. a successful sign-in.</summary>
        void Info(string message);

        /// <summary>Records something unexpected that the application handled, e.g. a rejected sign-in.</summary>
        void Warn(string message);

        /// <summary>Records a failure together with its exception.</summary>
        /// <param name="message">What the application was doing when it failed.</param>
        /// <param name="ex">The exception; may be null when there is none.</param>
        void Error(string message, Exception ex);
    }
}
