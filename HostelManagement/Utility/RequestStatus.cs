using System;

namespace HostelManagement.Utility
{
    /// <summary>
    /// Values stored in tbl_RequestRoom.Status.
    /// Lifecycle: Pending -> Approved | Rejected | Cancelled; Approved -> Vacated.
    /// </summary>
    public static class RequestStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
        public const string Vacated = "Vacated";

        /// <summary>Every status, in lifecycle order.</summary>
        public static readonly string[] All = { Pending, Approved, Rejected, Cancelled, Vacated };

        /// <summary>True for a value this application stores; use it to vet untrusted input such as a filter selection.</summary>
        public static bool IsKnown(string status)
        {
            return Array.IndexOf(All, status) >= 0;
        }
    }
}
