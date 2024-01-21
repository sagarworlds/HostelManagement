using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class RoomList : System.Web.UI.Page
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
            var oRoomDB = new RoomDB();
            var lstRoom = oRoomDB.GetRoomList();
            gvRooms.DataSource = lstRoom;
            gvRooms.DataBind();
        }



        protected void gvRooms_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            // Retrieve the index of the row being deleted
            int rowIndex = e.RowIndex;

            // Access the data key of the row to get the record ID
            int Id = Convert.ToInt32(gvRooms.DataKeys[rowIndex]["Id"]);

            var oRoomDB = new RoomDB();
            oRoomDB.DeleteRoom(Id);
            BindGrid();

        }
    }
}