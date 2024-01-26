using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class AdminList : System.Web.UI.Page
    {
        public string UserType   // property
        {
            get { return Convert.ToString(Request.QueryString["UserType"]); }
            set { UserType = value; }
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindGrid();
            }
            if (UserType == "Student")
            {
                title.InnerHtml = "User List";
                hlAddnew.Visible = false;
            }
        }
        private void BindGrid()
        {
            var oUserDB = new UserDB();
            var lstUser = oUserDB.GetAdminList(UserType);
            gvAdmin.DataSource = lstUser;
            gvAdmin.DataBind();
        }

        protected void gvAdmin_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            // Retrieve the index of the row being deleted
            int rowIndex = e.RowIndex;
            // Access the data key of the row to get the record ID
            int Id = Convert.ToInt32(gvAdmin.DataKeys[rowIndex]["Id"]);
            var oUserDB = new UserDB();
            oUserDB.DeleteAdminUser(Id);
            BindGrid();
        }
    }
}