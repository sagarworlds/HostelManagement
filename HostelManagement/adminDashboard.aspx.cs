using HostelManagement.Utility;
using System;

namespace HostelManagement
{
    public partial class adminDashboard : System.Web.UI.Page
    {
        private readonly IRequestRoomRepository _requests = AppServices.Requests();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
                BindGrid();
        }

        private void BindGrid()
        {
            gvRoomsRequest.DataSource = _requests.GetRoomRequestList();
            gvRoomsRequest.DataBind();
        }
    }
}
