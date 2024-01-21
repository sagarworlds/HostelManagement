using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class Register : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            btnCreateAccount.Click += BtnCreateAccount_Click;
        }

        private void BtnCreateAccount_Click(object sender, EventArgs e)
        {
            if (Page.IsValid)
            {
                var oUser = new User();
                oUser.FirstName = inputFirstName.Text;
                oUser.LastName = inputLastName.Text;
                oUser.Email = inputEmail.Text;
                oUser.Password = inputPassword.Text;
                oUser.UserType = "Student";
                oUser.MobileNo = "";
                var oUserDB = new UserDB();
                bool isSuccess = oUserDB.AddUser(oUser);
                string msg = string.Empty;
                if (isSuccess)
                {
                    msg = "Your registration is successful. Please login";
                }
                else
                {
                    msg = "Your registration is not successful. Please try again.";
                }
                string script = "<script type='text/javascript'>showModalmsg('" + msg + "');</script>";
                ClientScript.RegisterStartupScript(this.GetType(), "CallMyFunction", script, false);
            }

        }
    }
}