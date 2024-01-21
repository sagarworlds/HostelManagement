using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml.Linq;

namespace HostelManagement
{
    public partial class AddEditRoom : System.Web.UI.Page
    {
        public string Id   // property
        {
            get { return Convert.ToString(Request.QueryString["Id"]); }
            set { Id = value; }
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            btnCreateRoom.Click += BtnCreateRoom_Click;
            if (!IsPostBack)
            {
                if (!string.IsNullOrEmpty(Id))
                {
                    btnCreateRoom.Text = "Update Room";
                    GetRoom();
                }
            }
        }

        private void GetRoom()
        {
            var oRoomDB = new RoomDB();
            var oRoom = oRoomDB.GetRoomById(Convert.ToInt32(Id));
            txtRoomName.Text = oRoom.RoomName;
            txtRent.Text = Convert.ToString(oRoom.Rent);
            txtDescription.Text = oRoom.Description;
        }
        private void BtnCreateRoom_Click(object sender, EventArgs e)
        {
            if (Page.IsValid)
            {
                var oRoom = new Room();
                var oRoomDB = new RoomDB();
                oRoom.RoomName = txtRoomName.Text;
                oRoom.Rent = Convert.ToInt32(txtRent.Text);
                oRoom.Description = txtDescription.Text;
                oRoom.ModifiedBy = 1;
                string msg = string.Empty;
                bool isSuccess = false;
                if (!string.IsNullOrEmpty(Id))
                {
                    oRoom.Id= Convert.ToInt32(Id);
                    isSuccess = oRoomDB.UpdateRoom(oRoom);
                }
                else
                {
                    isSuccess = oRoomDB.AddRoom(oRoom);
                }
                if (isSuccess)
                {
                    //  msg = "Room Added successfully.";
                    Response.Redirect("~/RoomList.aspx");
                }
                else
                {
                    msg = "There is problem in adding room. Please try again.";

                    string script = "<script type='text/javascript'>showModalmsg('" + msg + "');</script>";
                    ClientScript.RegisterStartupScript(this.GetType(), "CallMyFunction", script, false);
                }

            }
        }
    }
}