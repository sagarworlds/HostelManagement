using HostelManagement.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class ApproveRoomRequest : System.Web.UI.Page
    {
        public string Id   // property
        {
            get { return Convert.ToString(Request.QueryString["Id"]); } 
            set { Id = value; }
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            if(!IsPostBack) {
                GetRoomRequest();
            }
        }

        private void GetRoomRequest()
        {
            RequestRoomDB oRequestRoomDB = new RequestRoomDB();
            int _id = Convert.ToInt32(Id);
            var response = oRequestRoomDB.GetRoomRequestListById(_id);
            lblRequestNo.Text = Convert.ToString(response.Id);
            lblStatus.Text = Convert.ToString(response.Status);
            lblUserName.Text = Convert.ToString(response.UserName);
        }
    }
}