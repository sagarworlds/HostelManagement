using HostelManagement.Utility;
using System;
using System.Collections.Generic;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class adminDashboard : System.Web.UI.Page
    {
        private readonly IRequestRoomRepository _requests = AppServices.Requests();
        private readonly IDashboardRepository _dashboard = AppServices.Dashboard();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
                return;

            ddlStatusFilter.Items.Add(new ListItem("All requests", string.Empty));
            foreach (string status in RequestStatus.All)
                ddlStatusFilter.Items.Add(new ListItem(status, status));

            BindStats();
            BindGrid();
        }

        protected void ddlStatusFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            BindGrid();
        }

        private void BindStats()
        {
            DashboardSummary summary = _dashboard.GetSummary();

            rptStats.DataSource = new List<StatTile>
            {
                new StatTile("Pending requests", summary.PendingRequests, "bg-warning text-dark", string.Empty),
                new StatTile("Total rooms", summary.TotalRooms, "bg-primary text-white", "RoomList.aspx"),
                new StatTile("Available rooms", summary.AvailableRooms, "bg-success text-white", "RoomList.aspx"),
                new StatTile("Occupied rooms", summary.OccupiedRooms, "bg-secondary text-white", "RoomList.aspx"),
                new StatTile("Students", summary.Students, "bg-info text-dark", "AdminList.aspx?UserType=Student"),
                new StatTile("Admins", summary.Admins, "bg-dark text-white", "AdminList.aspx?UserType=Admin")
            };
            rptStats.DataBind();
        }

        private void BindGrid()
        {
            // The selection arrives from the browser, so only a status the application stores is used as a filter.
            string selected = ddlStatusFilter.SelectedValue;
            string status = RequestStatus.IsKnown(selected) ? selected : null;

            gvRoomsRequest.DataSource = _requests.GetRoomRequestList(status);
            gvRoomsRequest.DataBind();
            gvRoomsRequest.RenderHeaderInThead();
        }
    }
}
