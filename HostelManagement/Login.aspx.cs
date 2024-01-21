using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class Login : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            btnLogin.Click += BtnLogin_Click;
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (Page.IsValid)
            {
                var oUser = new User();
                oUser.Email = inputEmail.Text;
                oUser.Password = inputPassword.Text;
                var oUserDB = new UserDB();
                var user = oUserDB.Authenticate(oUser);
                string msg = string.Empty;

                if (user != null)
                {
                    msg = "Login Successfull.";
                }
                else
                {
                    msg = "Email or Password is incorrect.";
                }

                string script = "<script type='text/javascript'>showModalmsg('" + msg + "');</script>";
                ClientScript.RegisterStartupScript(this.GetType(), "CallMyFunction", script, false);
            }
        }
    }
}