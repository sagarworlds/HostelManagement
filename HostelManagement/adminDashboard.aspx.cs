using HostelManagement.Utility;
using System;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class adminDashboard : System.Web.UI.Page
    {
        private readonly IRequestRoomRepository _requests = AppServices.Requests();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
                return;

            ddlStatusFilter.Items.Add(new ListItem("All requests", string.Empty));
            foreach (string status in RequestStatus.All)
                ddlStatusFilter.Items.Add(new ListItem(status, status));

            BindGrid();
        }

        protected void ddlStatusFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            BindGrid();
        }

        private void BindGrid()
        {
            // The selection arrives from the browser, so only a status the application stores is used as a filter.
            string selected = ddlStatusFilter.SelectedValue;
            string status = RequestStatus.IsKnown(selected) ? selected : null;

            gvRoomsRequest.DataSource = _requests.GetRoomRequestList(status);
            gvRoomsRequest.DataBind();
        }
    }
}
