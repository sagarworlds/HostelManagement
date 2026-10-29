using System;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class AdminList : System.Web.UI.Page
    {
        private const string ResetPasswordCommand = "ResetPassword";

        private readonly IUserRepository _users = AppServices.Users();
        private readonly ILogger _logger = AppServices.Logger();

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
            Title = isStudentList ? "Users" : "Admins";
            hlAddnew.Visible = !isStudentList;
            BindGrid();
        }

        private void BindGrid()
        {
            gvAdmin.DataSource = _users.GetUsersByType(UserType);
            gvAdmin.DataBind();
            gvAdmin.RenderHeaderInThead();
        }

        protected void gvAdmin_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            // The built-in Delete command also raises RowCommand; it is handled by RowDeleting.
            if (e.CommandName != ResetPasswordCommand || UserType != UserTypes.Student)
                return;

            int studentId = Convert.ToInt32(e.CommandArgument);
            string temporaryPassword = TemporaryPassword.Generate();

            OperationResult result = _users.ResetStudentPassword(studentId, temporaryPassword);
            if (!result.Success)
            {
                ClientMessage.Show(this, result.Message);
                return;
            }

            // Who reset whom is logged; the password itself is not. It is shown once, here, and stored only as a hash.
            _logger.Info("Administrator " + Convert.ToInt32(Session["UserId"]) + " reset the password of student " + studentId + ".");

            User student = _users.GetUserById(studentId);
            string name = student == null ? "the student" : student.UserName;
            ClientMessage.Show(this,
                "Temporary password for " + name + ": " + temporaryPassword +
                "  -  give it to the student. They must choose a new password when they sign in. It will not be shown again.");
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
