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

        public string Status { get; set; } = "Pending";

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public DateTime ModifiedOn { get; set; }= DateTime.Now;

        public int ModifiedBy { get; set; }

        public DateTime RequestDate { get; set; }=DateTime.Now;

        public DateTime ResponseDate { get; set; }

        public string RoomName { get; set; }
        public string UserName { get; set; }


    }
}