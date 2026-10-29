using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HostelManagement
{
    public class Room
    {
        public int Id { get; set; }
        public string RoomName { get; set; }
        public int Rent { get; set; }
        public string Description { get; set; }
        public int ModifiedBy { get; set; }
        public DateTime CreatedOn { get; set; }=DateTime.Now;
        public DateTime ModifiedOn { get; set; } = DateTime.Now;
        public bool Availability { get; set; }

        /// <summary>Name of the student who currently holds the room; empty when nobody does.</summary>
        public string OccupantName { get; set; }

        /// <summary>Id of the Approved request that allocates this room; 0 when the room is not allocated.</summary>
        public int ActiveRequestId { get; set; }

        public bool IsOccupied
        {
            get { return ActiveRequestId > 0; }
        }

        /// <summary>Text for the room list: who holds the room, or whether it can be allocated.</summary>
        public string OccupancyStatus
        {
            get
            {
                if (IsOccupied)
                    return "Occupied by " + OccupantName;

                return Availability ? "Available" : "Unavailable";
            }
        }
    }
}