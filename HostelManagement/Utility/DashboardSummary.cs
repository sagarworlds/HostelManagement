namespace HostelManagement
{
    /// <summary>Headline counts shown on the administrator dashboard.</summary>
    public class DashboardSummary
    {
        public int TotalRooms { get; set; }

        /// <summary>Rooms that can be allocated right now.</summary>
        public int AvailableRooms { get; set; }

        /// <summary>Rooms currently held by a student.</summary>
        public int OccupiedRooms { get; set; }

        /// <summary>Requests waiting for an administrator's decision.</summary>
        public int PendingRequests { get; set; }

        public int Students { get; set; }

        public int Admins { get; set; }
    }
}
