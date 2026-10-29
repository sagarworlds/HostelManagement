using System;
using System.Web.UI;

namespace HostelManagement
{
    /// <summary>
    /// Profile details and password change for the signed-in user. Administrators and students share this page;
    /// each sees it inside their own master page (and therefore their own navigation and access check).
    /// </summary>
    public partial class MyProfile : System.Web.UI.Page
    {
        private readonly IUserRepository _users = AppServices.Users();

        private int CurrentUserId
        {
            get { return Convert.ToInt32(Session["UserId"]); }
        }

        private bool IsAdmin
        {
            get { return Convert.ToString(Session["UserType"]) == UserTypes.Admin; }
        }

        protected void Page_PreInit(object sender, EventArgs e)
        {
            // Visitors who are not signed in get the student master, whose guard sends them to the login page.
            MasterPageFile = IsAdmin ? "~/Admin.Master" : "~/UserMaster.Master";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            btnSaveProfile.Click += BtnSaveProfile_Click;
            btnChangePassword.Click += BtnChangePassword_Click;
            if (IsPostBack)
                return;

            pnlMustChange.Visible = AccessGuard.MustChangePassword(Session);
            LoadProfile();
        }

        private void LoadProfile()
        {
            User user = _users.GetUserById(CurrentUserId);
            if (user == null)
            {
                // The account was deleted while this session was open.
                Session.Clear();
                Response.Redirect("~/Login.aspx");
                return;
            }

            txtEmail.Text = user.Email;
            txtFirstName.Text = user.FirstName;
            txtLastName.Text = user.LastName;
            txtMobile.Text = user.MobileNo;
        }

        private void BtnSaveProfile_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            var user = new User
            {
                Id = CurrentUserId,
                FirstName = txtFirstName.Text.Trim(),
                LastName = txtLastName.Text.Trim(),
                MobileNo = txtMobile.Text.Trim()
            };

            OperationResult result = _users.UpdateProfile(user);
            if (!result.Success)
            {
                ClientMessage.Show(this, result.Message);
                return;
            }

            Session["Name"] = user.FirstName + " " + user.LastName;

            // Redirect so the name in the navigation is refreshed and a browser refresh does not resubmit.
            ClientMessage.Flash(this, "Your profile has been updated.");
            Response.Redirect(AccessGuard.ProfileUrl);
        }

        private void BtnChangePassword_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            string policyProblem = PasswordPolicy.Validate(txtNewPassword.Text);
            if (policyProblem != null)
            {
                ClientMessage.Show(this, policyProblem);
                return;
            }

            if (txtNewPassword.Text == txtCurrentPassword.Text)
            {
                ClientMessage.Show(this, "Choose a password that is different from your current one.");
                return;
            }

            OperationResult result = _users.ChangePassword(CurrentUserId, txtCurrentPassword.Text, txtNewPassword.Text);
            if (!result.Success)
            {
                ClientMessage.Show(this, result.Message);
                return;
            }

            // Lifts the restriction that kept a reset user on this page.
            Session[AccessGuard.MustChangePasswordKey] = false;

            ClientMessage.Flash(this, "Your password has been changed.");
            Response.Redirect(IsAdmin ? "~/adminDashboard.aspx" : "~/UserDashboard.aspx");
        }
    }
}
