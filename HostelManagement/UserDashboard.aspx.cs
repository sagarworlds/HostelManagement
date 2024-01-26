using HostelManagement.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class UserDashboard : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            btnAddNewRequest.Click += BtnAddNewRequest_Click;
            if (!IsPostBack)
            {
                BindGrid();
            }
        }

        private void BtnAddNewRequest_Click(object sender, EventArgs e)
        {
            RequestRoomDB oRequestRoomDB = new RequestRoomDB();
            RequestRoom oRequestRoom = new RequestRoom();
            int UserId = Convert.ToInt32(Session["UserId"]); ;
            oRequestRoom.UserId = UserId;
            oRequestRoomDB.AddRoomRequest(oRequestRoom);
            BindGrid();
        }
        private void BindGrid()
        {
            RequestRoomDB oRequestRoomDB = new RequestRoomDB();
            int UserId = Convert.ToInt32(Session["UserId"]); ;

            var lstRoom = oRequestRoomDB.GetRoomRequestListByUserId(UserId);
            gvRoomsRequest.DataSource = lstRoom;
            gvRoomsRequest.DataBind();
        }
       

       
    }
}