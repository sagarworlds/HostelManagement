using HostelManagement.Utility;
using System;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class AvailableRooms : System.Web.UI.Page
    {
        private const string RequestCommand = "RequestRoom";

        private readonly IRoomRepository _rooms = AppServices.Rooms();
        private readonly IRequestRoomRepository _requests = AppServices.Requests();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
                BindGrid();
        }

        private void BindGrid()
        {
            gvRooms.DataSource = _rooms.GetAvailableRooms();
            gvRooms.DataBind();
            gvRooms.RenderHeaderInThead();
        }

        protected void gvRooms_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != RequestCommand)
                return;

            var request = new RequestRoom
            {
                UserId = Convert.ToInt32(Session["UserId"]),
                PreferredRoomId = Convert.ToInt32(e.CommandArgument)
            };

            OperationResult result = _requests.AddRoomRequest(request);
            if (!result.Success)
            {
                ClientMessage.Show(this, result.Message);
                BindGrid();
                return;
            }

            ClientMessage.Flash(this, "Your request has been sent. An administrator will review it.");
            Response.Redirect("~/UserDashboard.aspx");
        }
    }
}
