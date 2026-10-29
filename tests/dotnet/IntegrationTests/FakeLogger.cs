using System;
using System.Collections.Generic;

namespace HostelManagement.IntegrationTests
{
    /// <summary>Collects log entries so tests can assert that a failure was logged rather than swallowed.</summary>
    public sealed class FakeLogger : ILogger
    {
        private readonly object _gate = new object();
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
            lock (_gate)
                _warnings.Add(message);
        }

        public void Error(string message, Exception ex)
        {
            lock (_gate)
                _errors.Add(ex == null ? message : message + " | " + ex.Message);
        }

        public IReadOnlyList<string> Errors
        {
            get
            {
                lock (_gate)
                    return _errors.ToArray();
            }
        }

        public IReadOnlyList<string> Warnings
        {
            get
            {
                lock (_gate)
                    return _warnings.ToArray();
            }
        }
    }
}
