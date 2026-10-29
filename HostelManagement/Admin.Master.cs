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
            AccessGuard.Require(Page, UserTypes.Admin);
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            string userName = Convert.ToString(Session["Name"]);
            lblLoggedInUser.Text = Server.HtmlEncode(userName);
        }

       
        protected void lnkLogout_Click(object sender, EventArgs e)
        {
            LoginBinding.Clear(Page);
            Session.Abandon();
            Response.Redirect("~/Login.aspx");

        }
    }
}