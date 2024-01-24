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

        
    }
}