using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class Admin : System.Web.UI.MasterPage
    {
        protected void Page_Init(object sender, EventArgs e)
        {
            if (Session["UserId"] == null || Convert.ToString(Session["UserType"]) != "Admin")
            {
                Response.Redirect("~/Login.aspx");
            }
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            string userName = Convert.ToString(Session["Name"]);
            lblLoggedInUser.Text = userName;
        }

       
        protected void lnkLogout_Click(object sender, EventArgs e)
        {
            Session.Abandon();
            Response.Redirect("~/Login.aspx");

        }
    }
}