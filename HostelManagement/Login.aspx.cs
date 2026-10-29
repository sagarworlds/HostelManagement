using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class Login : System.Web.UI.Page
    {
        private readonly IUserRepository _users = AppServices.Users();
        private readonly ILogger _logger = AppServices.Logger();

        protected void Page_Load(object sender, EventArgs e)
        {
            btnLogin.Click += BtnLogin_Click;
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            User user;
            try
            {
                user = _users.Authenticate(inputEmail.Text.Trim(), inputPassword.Text);
            }
            catch (DataAccessException)
            {
                // The repository has already logged the cause.
                ClientMessage.Show(this, ClientMessage.ServiceUnavailable);
                return;
            }

            if (user == null)
            {
                ClientMessage.Show(this, "Email or Password is incorrect.");
                return;
            }

            string dashboard = DashboardFor(user.UserType);
            if (dashboard == null)
            {
                _logger.Warn("User " + user.Id + " has the unknown account type '" + user.UserType + "' and was not signed in.");
                ClientMessage.Show(this, "Your account has no valid role. Please contact the administrator.");
                return;
            }

            // Drop anything left in the session from before sign-in so only the identity set below is trusted.
            Session.Clear();
            Session["UserId"] = user.Id;
            Session["UserType"] = user.UserType;
            Session["Name"] = user.FirstName + " " + user.LastName;
            Session[AccessGuard.MustChangePasswordKey] = user.MustChangePassword;

            _logger.Info("User " + user.Id + " signed in as " + user.UserType + ".");

            // After an administrator reset the password, the only way forward is choosing a new one.
            Response.Redirect(user.MustChangePassword ? AccessGuard.ProfileUrl : dashboard);
        }

        /// <returns>The landing page for the account type, or null when the type is not one the application knows.</returns>
        private static string DashboardFor(string userType)
        {
            if (userType == UserTypes.Student)
                return "~/UserDashboard.aspx";
            if (userType == UserTypes.Admin)
                return "~/adminDashboard.aspx";

            return null;
        }
    }
}
