using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class Login : System.Web.UI.Page
    {
        private readonly IUserRepository _users = AppServices.Users();
        private readonly ILogger _logger = AppServices.Logger();
        private readonly LoginAttemptPolicy _attempts = AppServices.LoginAttempts();

        protected void Page_Load(object sender, EventArgs e)
        {
            btnLogin.Click += BtnLogin_Click;

            // Offered only once an administrator has set up the mail server.
            if (!IsPostBack)
                lnkForgot.Visible = AppServices.PasswordReset().IsEnabled;
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            string email = inputEmail.Text.Trim();
            string address = Request.UserHostAddress;

            TimeSpan wait = _attempts.RetryAfter(email, address);
            if (wait > TimeSpan.Zero)
            {
                int minutes = (int)Math.Ceiling(wait.TotalMinutes);
                _logger.Warn("Sign-in blocked for '" + email + "' from " + address + " after repeated failures.");
                ClientMessage.Show(this, "Too many failed attempts. Please try again in " + minutes + (minutes == 1 ? " minute." : " minutes."));
                return;
            }

            User user;
            try
            {
                user = _users.Authenticate(email, inputPassword.Text);
            }
            catch (DataAccessException)
            {
                // The repository has already logged the cause.
                ClientMessage.Show(this, ClientMessage.ServiceUnavailable);
                return;
            }

            if (user == null)
            {
                // Unknown e-mails count too, so the limit never reveals which accounts exist.
                _attempts.RecordFailure(email, address);
                ClientMessage.Show(this, "Email or Password is incorrect.");
                return;
            }

            _attempts.RecordSuccess(email, address);

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
            LoginBinding.Establish(this);

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
