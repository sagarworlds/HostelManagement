using HostelManagement.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class adminDashboard : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindGrid();
            }
        }

        private void BindGrid()
        {
            RequestRoomDB oRequestRoomDB = new RequestRoomDB();
            var lstRoom = oRequestRoomDB.GetRoomRequestList();
            gvRoomsRequest.DataSource = lstRoom;
            gvRoomsRequest.DataBind();
        }
        protected void gvRoomsRequest_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Approve")
            {
                int rowIndex = Convert.ToInt32(e.CommandArgument);
                GridViewRow row = gvRoomsRequest.Rows[rowIndex];

                // Retrieve data
                int requestId = Convert.ToInt32(gvRoomsRequest.DataKeys[row.RowIndex].Value);
                string selectedStatus = ((DropDownList)row.FindControl("ddlStatus")).SelectedValue;

                // Perform your logic to update the status in the database using requestId and selectedStatus

                // Reload or bind the GridView after updating the database
                BindGrid(); // You should implement this method to bind data to the GridView
            }
        }
    }
}