using HostelManagement.Utility;
using System;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class UserDashboard : System.Web.UI.Page
    {
        private const string CancelCommand = "CancelRequest";
        private const string DashboardUrl = "~/UserDashboard.aspx";

        private const int AnnouncementsShown = 5;

        private readonly IRequestRoomRepository _requests = AppServices.Requests();
        private readonly IAnnouncementRepository _announcements = AppServices.Announcements();

        private int CurrentUserId
        {
            get { return Convert.ToInt32(Session["UserId"]); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            btnAddNewRequest.Click += BtnAddNewRequest_Click;
            if (!IsPostBack)
            {
                BindAnnouncements();
                BindGrid();
            }
        }

        private void BindAnnouncements()
        {
            var current = _announcements.GetActive(AnnouncementsShown, DateTime.Now);

            // With nothing to say, the whole section (heading included) is left out.
            phAnnouncements.Visible = current.Count > 0;
            rptAnnouncements.DataSource = current;
            rptAnnouncements.DataBind();
        }

        private void BtnAddNewRequest_Click(object sender, EventArgs e)
        {
            Respond(_requests.AddRoomRequest(new RequestRoom { UserId = CurrentUserId }));
        }

        protected void gvRoomsRequest_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != CancelCommand)
                return;

            Respond(_requests.CancelRequest(Convert.ToInt32(e.CommandArgument), CurrentUserId));
        }

        private void Respond(OperationResult result)
        {
            if (result.Success)
            {
                // Redirect (rather than rebind) so refreshing the browser does not repeat the action.
                Response.Redirect(DashboardUrl);
                return;
            }

            ClientMessage.Show(this, result.Message);
            BindGrid();
        }

        private void BindGrid()
        {
            gvRoomsRequest.DataSource = _requests.GetRoomRequestListByUserId(CurrentUserId);
            gvRoomsRequest.DataBind();
        }
    }
}
