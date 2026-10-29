using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class Register : System.Web.UI.Page
    {
        private readonly IUserRepository _users = AppServices.Users();

        protected void Page_Load(object sender, EventArgs e)
        {
            btnCreateAccount.Click += BtnCreateAccount_Click;
        }

        private void BtnCreateAccount_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            var user = new User
            {
                FirstName = inputFirstName.Text.Trim(),
                LastName = inputLastName.Text.Trim(),
                Email = inputEmail.Text.Trim(),
                Password = inputPassword.Text,
                UserType = UserTypes.Student,
                MobileNo = string.Empty
            };

            string passwordProblem = PasswordPolicy.Validate(user.Password);
            if (passwordProblem != null)
            {
                ClientMessage.Show(this, passwordProblem);
                return;
            }

            try
            {
                if (_users.EmailExists(user.Email, 0))
                {
                    ClientMessage.Show(this, "This email already exists. Register with a different email address.");
                    return;
                }

                OperationResult result = _users.AddUser(user);
                if (result.Success)
                    ClientMessage.Show(this, "Your registration is successful. Please login.", "~/Login.aspx");
                else
                    ClientMessage.Show(this, result.Message);
            }
            catch (DataAccessException)
            {
                // The repository has already logged the cause.
                ClientMessage.Show(this, ClientMessage.ServiceUnavailable);
            }
        }
    }
}
