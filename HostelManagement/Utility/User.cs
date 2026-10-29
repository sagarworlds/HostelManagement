using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HostelManagement
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string UserType { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MobileNo { get; set; }

        /// <summary>True after an administrator reset the password: the user must choose a new one before using the application.</summary>
        public bool MustChangePassword { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public DateTime ModifiedOn { get; set; } = DateTime.Now;
        public string UserName { get; set; }

    }
}