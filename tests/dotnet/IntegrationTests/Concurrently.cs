using System;
using System.Collections.Generic;
using System.Threading;

namespace HostelManagement.IntegrationTests
{
    /// <summary>Runs actions at the same moment on separate threads (and so separate connections), to provoke races.</summary>
    public static class Concurrently
    {
        /// <summary>
        /// Starts one thread per action, holds them at a barrier so they begin together, and returns every result.
        /// An exception on any thread is rethrown here rather than lost.
        /// </summary>
        public static List<T> Run<T>(params Func<T>[] actions)
        {
            var results = new T[actions.Length];
            var failures = new List<Exception>();
            var threads = new List<Thread>();

            using (var barrier = new Barrier(actions.Length))
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    int index = i;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            barrier.SignalAndWait();
                            results[index] = actions[index]();
                        }
                        catch (Exception ex)
                        {
                            lock (failures)
                                failures.Add(ex);
                        }
                    });
                    threads.Add(thread);
                    thread.Start();
                }

                foreach (Thread thread in threads)
                    thread.Join();
            }

            if (failures.Count > 0)
                throw new AggregateException("An action failed while running concurrently.", failures);

            return new List<T>(results);
        }
    }
}
