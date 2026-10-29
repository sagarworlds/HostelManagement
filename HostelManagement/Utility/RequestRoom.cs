using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HostelManagement.Utility
{
    public class RequestRoom
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ApprovedRoomId { get; set; }

        /// <summary>The room the student asked for; 0 when they expressed no preference. Only a preference: the administrator chooses the room.</summary>
        public int PreferredRoomId { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public DateTime ModifiedOn { get; set; }= DateTime.Now;

        public int ModifiedBy { get; set; }

        public DateTime RequestDate { get; set; }=DateTime.Now;

        public DateTime ResponseDate { get; set; }

        public string RoomName { get; set; }

        /// <summary>Name of the preferred room; empty when there is none.</summary>
        public string PreferredRoomName { get; set; }
        public string UserName { get; set; }


    }
}