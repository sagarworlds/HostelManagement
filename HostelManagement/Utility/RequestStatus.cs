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
    }
}
