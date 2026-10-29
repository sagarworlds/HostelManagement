using HostelManagement.Utility;
using System;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class RoomList : System.Web.UI.Page
    {
        private const string VacateCommand = "Vacate";

        private readonly IRoomRepository _rooms = AppServices.Rooms();
        private readonly IRequestRoomRepository _requests = AppServices.Requests();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
                BindGrid();
        }

        private void BindGrid()
        {
            gvRooms.DataSource = _rooms.GetRoomList();
            gvRooms.DataBind();
        }

        protected void gvRooms_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            int id = Convert.ToInt32(gvRooms.DataKeys[e.RowIndex]["Id"]);

            Report(_rooms.DeleteRoom(id));
        }

        protected void gvRooms_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            // The built-in Delete command also raises RowCommand; it is handled by RowDeleting.
            if (e.CommandName != VacateCommand)
                return;

            int requestId = Convert.ToInt32(e.CommandArgument);
            Report(_requests.VacateRoom(requestId, Convert.ToInt32(Session["UserId"])));
        }

        private void Report(OperationResult result)
        {
            if (!result.Success)
                ClientMessage.Show(this, result.Message);

            BindGrid();
        }
    }
}
