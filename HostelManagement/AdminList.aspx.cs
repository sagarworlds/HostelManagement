using System;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class AdminList : System.Web.UI.Page
    {
        private readonly IUserRepository _users = AppServices.Users();

        /// <summary>
        /// Account type being listed. Read from the query string but restricted to the known types
        /// (default: Admin), so a missing or tampered value can never produce an empty or wrong list.
        /// </summary>
        public string UserType
        {
            get { return UserTypes.Normalize(Request.QueryString["UserType"]); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
                return;

            // Students register themselves, so only the admin list offers an "Add" button.
            bool isStudentList = UserType == UserTypes.Student;
            title.InnerText = isStudentList ? "User List" : "Admin List";
            hlAddnew.Visible = !isStudentList;
            BindGrid();
        }

        private void BindGrid()
        {
            gvAdmin.DataSource = _users.GetUsersByType(UserType);
            gvAdmin.DataBind();
        }

        protected void gvAdmin_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            int id = Convert.ToInt32(gvAdmin.DataKeys[e.RowIndex]["Id"]);

            // Each type has its own delete with its own rules; the repository also checks the stored type.
            OperationResult result = UserType == UserTypes.Student
                ? _users.DeleteStudent(id)
                : _users.DeleteAdmin(id, Convert.ToInt32(Session["UserId"]));

            if (!result.Success)
                ClientMessage.Show(this, result.Message);

            BindGrid();
        }
    }
}
