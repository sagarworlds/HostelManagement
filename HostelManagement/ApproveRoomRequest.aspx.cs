using HostelManagement.Utility;
using System;
using System.Collections.Generic;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class ApproveRoomRequest : System.Web.UI.Page
    {
        private const string DashboardUrl = "~/adminDashboard.aspx";

        private readonly IRequestRoomRepository _requests = AppServices.Requests();
        private readonly IRoomRepository _rooms = AppServices.Rooms();

        private int RequestId
        {
            get
            {
                int id;
                return int.TryParse(Request.QueryString["Id"], out id) ? id : 0;
            }
        }

        private int CurrentAdminId
        {
            get { return Convert.ToInt32(Session["UserId"]); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            btnApprove.Click += BtnApprove_Click;
            btnReject.Click += BtnReject_Click;
            if (IsPostBack)
                return;

            RequestRoom request = _requests.GetRoomRequestById(RequestId);
            if (request == null || request.Status != RequestStatus.Pending)
            {
                // Only a Pending request can be decided; a stale link or a second administrator lands here.
                ClientMessage.Flash(this, request == null ? "That request no longer exists." : "That request has already been processed.");
                Response.Redirect(DashboardUrl);
                return;
            }

            // User-supplied text is encoded because Label.Text is written to the page as HTML.
            lblRequestNo.Text = Convert.ToString(request.Id);
            lblStatus.Text = Server.HtmlEncode(request.Status);
            lblUserName.Text = Server.HtmlEncode(request.UserName);
            BindAvailableRooms();
        }

        private void BtnApprove_Click(object sender, EventArgs e)
        {
            // The validator in the "approve" group already blocks the placeholder item; this also covers a posted
            // value that was never in the list.
            int roomId;
            if (!Page.IsValid || !int.TryParse(ddlAvailableRooms.SelectedValue, out roomId) || roomId <= 0)
            {
                ClientMessage.Show(this, "Select a room to allocate.");
                return;
            }

            Complete(_requests.ApproveRequest(RequestId, roomId, CurrentAdminId), "Request approved.");
        }

        private void BtnReject_Click(object sender, EventArgs e)
        {
            Complete(_requests.RejectRequest(RequestId, CurrentAdminId), "Request rejected.");
        }

        private void Complete(OperationResult result, string successMessage)
        {
            if (result.Success)
            {
                ClientMessage.Flash(this, successMessage);
                Response.Redirect(DashboardUrl);
                return;
            }

            ClientMessage.Show(this, result.Message);

            // The room that was picked may be the one that just went, so offer the current list.
            BindAvailableRooms();
        }

        private void BindAvailableRooms()
        {
            List<Room> rooms = _rooms.GetAvailableRooms();
            ddlAvailableRooms.DataSource = rooms;
            ddlAvailableRooms.DataBind();
            ddlAvailableRooms.Items.Insert(0, new ListItem(rooms.Count > 0 ? "Select Room" : "No rooms available", "0"));

            // With no free room the request can only be rejected.
            btnApprove.Enabled = rooms.Count > 0;
        }
    }
}
