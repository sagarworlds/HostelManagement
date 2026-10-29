using System;
using System.Collections.Generic;

namespace HostelManagement
{
    /// <summary>
    /// Counts failures per key in a sliding time window and says how long a key must wait once it has used up its
    /// allowance. Held in memory: it resets when the application restarts, which is acceptable for slowing down
    /// password guessing and keeps the feature free of schema changes.
    /// </summary>
    public class LoginThrottle
    {
        // Bounds memory when someone floods the page with made-up keys. Beyond this, new keys are not tracked at all
        // (throttling fails open for them) rather than letting the table grow without limit.
        private const int MaximumTrackedKeys = 20000;
        private const int SweepEveryCalls = 500;

        private readonly int _maximumFailures;
        private readonly TimeSpan _window;
        private readonly Func<DateTime> _now;
        private readonly object _sync = new object();
        private readonly Dictionary<string, List<DateTime>> _failures = new Dictionary<string, List<DateTime>>();
        private int _calls;

        /// <param name="maximumFailures">Failures allowed inside <paramref name="window"/> before the key is blocked.</param>
        /// <param name="window">How long a failure counts.</param>
        /// <param name="now">Clock; injected so tests do not have to wait.</param>
        public LoginThrottle(int maximumFailures, TimeSpan window, Func<DateTime> now)
        {
            if (maximumFailures < 1)
                throw new ArgumentOutOfRangeException("maximumFailures", "At least one failure must be allowed.");
            if (window <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("window", "The window must be positive.");
            if (now == null)
                throw new ArgumentNullException("now");

            _maximumFailures = maximumFailures;
            _window = window;
            _now = now;
        }

        /// <returns>Zero when the key may try; otherwise how long until it may.</returns>
        public TimeSpan RetryAfter(string key)
        {
            lock (_sync)
            {
                List<DateTime> failures = PruneAndGet(key);
                if (failures == null || failures.Count < _maximumFailures)
                    return TimeSpan.Zero;

                // Blocked until enough of the oldest failures have aged out to leave fewer than the maximum.
                DateTime allowedAt = failures[failures.Count - _maximumFailures] + _window;
                TimeSpan wait = allowedAt - _now();
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }
        }

        public void RecordFailure(string key)
        {
            lock (_sync)
            {
                SweepOccasionally();

                List<DateTime> failures = PruneAndGet(key);
                if (failures == null)
                {
                    if (_failures.Count >= MaximumTrackedKeys)
                        return;

                    failures = new List<DateTime>();
                    _failures[key] = failures;
                }

                failures.Add(_now());
            }
        }

        /// <summary>Forgets a key's failures, e.g. after that same user and address signed in successfully.</summary>
        public void Reset(string key)
        {
            lock (_sync)
            {
                _failures.Remove(key);
            }
        }

        private List<DateTime> PruneAndGet(string key)
        {
            List<DateTime> failures;
            if (!_failures.TryGetValue(key, out failures))
                return null;

            DateTime cutoff = _now() - _window;
            failures.RemoveAll(moment => moment <= cutoff);
            if (failures.Count == 0)
            {
                _failures.Remove(key);
                return null;
            }

            return failures;
        }

        private void SweepOccasionally()
        {
            _calls++;
            if (_calls % SweepEveryCalls != 0)
                return;

            DateTime cutoff = _now() - _window;
            var expired = new List<string>();
            foreach (KeyValuePair<string, List<DateTime>> entry in _failures)
            {
                if (entry.Value.Count == 0 || entry.Value[entry.Value.Count - 1] <= cutoff)
                    expired.Add(entry.Key);
            }

            foreach (string key in expired)
                _failures.Remove(key);
        }
    }
}
