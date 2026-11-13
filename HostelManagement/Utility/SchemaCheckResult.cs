using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HostelManagement
{
    /// <summary>What <see cref="ISchemaVerifier"/> found: whether the database has everything this version of the application needs.</summary>
    public sealed class SchemaCheckResult
    {
        public SchemaCheckResult(IEnumerable<string> problems)
        {
            Problems = new ReadOnlyCollection<string>(problems.ToList());
        }

        /// <summary>One sentence per missing table or column; empty when the database is current.</summary>
        public IList<string> Problems { get; private set; }

        public bool IsCurrent
        {
            get { return Problems.Count == 0; }
        }
    }
}
