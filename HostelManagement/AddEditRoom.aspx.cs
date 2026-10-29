using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class AddEditRoom : System.Web.UI.Page
    {
        private const int MaxDescriptionLength = 500;

        private readonly IRoomRepository _rooms = AppServices.Rooms();

        /// <summary>Id of the room being edited; 0 when a new room is being added.</summary>
        private int EditId
        {
            get
            {
                int id;
                return int.TryParse(Request.QueryString["Id"], out id) && id > 0 ? id : 0;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            btnCreateRoom.Click += BtnCreateRoom_Click;
            if (IsPostBack || EditId == 0)
                return;

            Room room = _rooms.GetRoomById(EditId);
            if (room == null)
            {
                ClientMessage.Flash(this, "That room no longer exists.");
                Response.Redirect("~/RoomList.aspx");
                return;
            }

            btnCreateRoom.Text = "Update Room";
            txtRoomName.Text = room.RoomName;
            txtRent.Text = Convert.ToString(room.Rent);
            txtDescription.Text = room.Description;
        }

        private void BtnCreateRoom_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            int rent;
            if (!int.TryParse(txtRent.Text, out rent) || rent < 0)
            {
                ClientMessage.Show(this, "Enter the rent as a whole number, 0 or more.");
                return;
            }

            string description = txtDescription.Text.Trim();
            if (description.Length > MaxDescriptionLength)
            {
                ClientMessage.Show(this, "The description can be at most " + MaxDescriptionLength + " characters.");
                return;
            }

            var room = new Room
            {
                Id = EditId,
                RoomName = txtRoomName.Text.Trim(),
                Rent = rent,
                Description = description,
                ModifiedBy = Convert.ToInt32(Session["UserId"])
            };

            OperationResult result = EditId > 0 ? _rooms.UpdateRoom(room) : _rooms.AddRoom(room);
            if (!result.Success)
            {
                ClientMessage.Show(this, result.Message);
                return;
            }

            Response.Redirect("~/RoomList.aspx");
        }
    }
}
