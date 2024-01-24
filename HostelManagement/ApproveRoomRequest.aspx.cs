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
            if (!IsPostBack)
            {
                BindAvaialbleRooms();
                GetRoomRequest();
            }
            btnApprove.Click += BtnApprove_Click;
        }

        private void BtnApprove_Click(object sender, EventArgs e)
        {
            var oRoom = new Room();
            oRoom.Availability = false;
            oRoom.ModifiedBy = Convert.ToInt32(Session["UserId"]);
            oRoom.Id= Convert.ToInt32(ddlAvailableRooms.SelectedValue);
            RoomDB oRoomDB = new RoomDB();
            var result = oRoomDB.UpdateRoomAvailablity(oRoom);
            if (result)
            {
                RequestRoom oRequestRoom = new RequestRoom();
                oRequestRoom.ApprovedRoomId = Convert.ToInt32(ddlAvailableRooms.SelectedValue);
                oRequestRoom.Status = "Approved";
                oRequestRoom.ResponseDate = DateTime.Now;
                oRequestRoom.Id = Convert.ToInt32( Id);
                oRequestRoom.ModifiedBy = Convert.ToInt32(Session["UserId"]);
                RequestRoomDB oRequestRoomDB = new RequestRoomDB();
                oRequestRoomDB.UpdateRoomRequestStatus(oRequestRoom);
            }

        }

        private void GetRoomRequest()
        {
            RequestRoomDB oRequestRoomDB = new RequestRoomDB();
            int _id = Convert.ToInt32(Id);
            var response = oRequestRoomDB.GetRoomRequestListById(_id);
            if (response != null)
            {
                lblRequestNo.Text = Convert.ToString(response.Id);
                lblStatus.Text = Convert.ToString(response.Status);
                lblUserName.Text = Convert.ToString(response.UserName);
            }
        }

        private void BindAvaialbleRooms()
        {
            RoomDB roomDB = new RoomDB();
            var lstRoom = roomDB.GetAvaialbleRoomList();
            if (lstRoom != null && lstRoom.Count > 0)
            {
                ddlAvailableRooms.DataSource = lstRoom;
                ddlAvailableRooms.DataBind();
                ddlAvailableRooms.Items.Insert(0, new ListItem("Select Room", "0"));
            }
        }

    }
}