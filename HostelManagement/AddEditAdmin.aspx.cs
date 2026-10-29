using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class AddEditAdmin : System.Web.UI.Page
    {
        private readonly IUserRepository _users = AppServices.Users();

        /// <summary>Id of the account being edited; 0 when a new administrator is being added.</summary>
        private int EditId
        {
            get
            {
                int id;
                return int.TryParse(Request.QueryString["Id"], out id) && id > 0 ? id : 0;
            }
        }

        /// <summary>Type of the account being added or edited; restricted to the known types (default: Admin).</summary>
        private string UserType
        {
            get { return UserTypes.Normalize(Request.QueryString["UserType"]); }
        }

        private string ListUrl
        {
            get { return "~/AdminList.aspx?UserType=" + UserType; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            btnCreateAccount.Click += BtnCreateAccount_Click;
            if (IsPostBack)
                return;

            if (EditId > 0)
            {
                EnterEditMode();
            }
            else if (UserType == UserTypes.Student)
            {
                // Students create their own account on the Register page; here they can only be edited.
                Response.Redirect(ListUrl);
            }
        }

        private void EnterEditMode()
        {
            User user = _users.GetUserById(EditId);
            if (user == null || user.UserType != UserType)
            {
                ClientMessage.Flash(this, "That account no longer exists.");
                Response.Redirect(ListUrl);
                return;
            }

            bool isStudent = UserType == UserTypes.Student;
            title.InnerText = isStudent ? "Edit User" : "Edit Admin";
            btnCreateAccount.Text = isStudent ? "Update User" : "Update Admin";

            // Passwords are not changed from this page.
            pnlPassword.Visible = false;

            inputFirstName.Text = user.FirstName;
            inputLastName.Text = user.LastName;
            inputEmail.Text = user.Email;
        }

        private void BtnCreateAccount_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            string email = inputEmail.Text.Trim();
            OperationResult result;
            try
            {
                // Ignore the account being edited so keeping its own e-mail address is not reported as a duplicate.
                if (_users.EmailExists(email, EditId))
                {
                    ClientMessage.Show(this, "This email already exists. Use a different email address.");
                    return;
                }

                result = EditId > 0 ? UpdateAccount(email) : AddAdmin(email);
            }
            catch (DataAccessException)
            {
                // The repository has already logged the cause.
                ClientMessage.Show(this, ClientMessage.ServiceUnavailable);
                return;
            }

            if (!result.Success)
            {
                ClientMessage.Show(this, result.Message);
                return;
            }

            Response.Redirect(ListUrl);
        }

        private OperationResult AddAdmin(string email)
        {
            return _users.AddUser(new User
            {
                FirstName = inputFirstName.Text.Trim(),
                LastName = inputLastName.Text.Trim(),
                Email = email,
                Password = inputPassword.Text,
                UserType = UserTypes.Admin,
                MobileNo = string.Empty
            });
        }

        private OperationResult UpdateAccount(string email)
        {
            var user = new User
            {
                Id = EditId,
                FirstName = inputFirstName.Text.Trim(),
                LastName = inputLastName.Text.Trim(),
                Email = email,
                UserType = UserType
            };

            OperationResult result = _users.UpdateUser(user);

            // Keep the name shown in the sidebar current when an administrator edits themselves.
            if (result.Success && EditId == Convert.ToInt32(Session["UserId"]))
                Session["Name"] = user.FirstName + " " + user.LastName;

            return result;
        }
    }
}
