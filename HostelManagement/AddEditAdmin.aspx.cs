using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class AddEditAdmin : System.Web.UI.Page
    {
        public string Id  
        {
            get { return Convert.ToString(Request.QueryString["Id"]); }
            set { Id = value; }
        }
        public string userType   
        {
            get { return Convert.ToString(Request.QueryString["userType"]); }
            set { userType = value; }
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            btnCreateAccount.Click += BtnCreateAccount_Click;
            if (!IsPostBack)
            {
                if (!string.IsNullOrEmpty(Id))
                {
                    if (userType == "Student")
                    {
                        title.InnerHtml = "Add/Edit User";
                        btnCreateAccount.Text = "Update User";
                    }
                    else
                    {
                        title.InnerHtml = "Add/Edit Admin";
                        btnCreateAccount.Text = "Update Admin";
                    }
                    pnlPassword.Visible = false;
                    GetUser();
                }
            }
        }
        private void GetUser()
        {
            var oUserDB = new UserDB();
            var oUser = oUserDB.GetUserById(Convert.ToInt32(Id));
            inputFirstName.Text = oUser.FirstName;
            inputLastName.Text = oUser.LastName;
            inputEmail.Text = oUser.Email;
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
                oUser.UserType = "Admin";
                oUser.MobileNo = "";
                //check if already email exists
                var oUserDB = new UserDB();
                string msg = string.Empty;
                bool isSuccess = false;
                if (oUserDB.EmailExists(oUser))
                {
                    msg = "This email is already exists. Register with different email Id.";
                }
                else
                {
                    isSuccess = oUserDB.AddUser(oUser);
                    if (isSuccess)
                    {
                        Response.Redirect("~/AdminList.aspx");
                    }
                    else
                    {
                        msg = "Your registration is not successful. Please try again.";
                    }
                }
                string script = "<script type='text/javascript'>showModalmsg('" + msg + "');</script>";
                ClientScript.RegisterStartupScript(this.GetType(), "CallMyFunction", script, false);
            }
        }
    }
}