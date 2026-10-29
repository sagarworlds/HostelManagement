using System;
using System.Globalization;
using System.Web.UI.WebControls;

namespace HostelManagement
{
    public partial class Announcements : System.Web.UI.Page
    {
        private const int MaximumTitleLength = 150;
        private const int MaximumBodyLength = 2000;

        private readonly IAnnouncementRepository _announcements = AppServices.Announcements();

        protected void Page_Load(object sender, EventArgs e)
        {
            btnPost.Click += BtnPost_Click;
            if (!IsPostBack)
                BindGrid();
        }

        private void BindGrid()
        {
            gvAnnouncements.DataSource = _announcements.GetAll();
            gvAnnouncements.DataBind();
            gvAnnouncements.RenderHeaderInThead();
        }

        private void BtnPost_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            string title = txtTitle.Text.Trim();
            string body = txtBody.Text.Trim();
            if (title.Length > MaximumTitleLength || body.Length > MaximumBodyLength)
            {
                ClientMessage.Show(this, "The title can be at most " + MaximumTitleLength + " characters and the message at most " + MaximumBodyLength + ".");
                return;
            }

            DateTime? lastDay = null;
            if (!string.IsNullOrWhiteSpace(txtLastDay.Text))
            {
                // A date input always posts yyyy-MM-dd, whatever the browser's locale.
                DateTime parsed;
                if (!DateTime.TryParseExact(txtLastDay.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                {
                    ClientMessage.Show(this, "Enter the last day as a date.");
                    return;
                }

                if (parsed.Date < DateTime.Today)
                {
                    ClientMessage.Show(this, "The last day cannot be in the past.");
                    return;
                }

                lastDay = parsed;
            }

            OperationResult result = _announcements.Add(new Announcement
            {
                Title = title,
                Body = body,
                CreatedBy = Convert.ToInt32(Session["UserId"]),
                CreatedOn = DateTime.Now,
                ExpiresOn = Announcement.ExpiryAfterLastDay(lastDay)
            });

            if (!result.Success)
            {
                ClientMessage.Show(this, result.Message);
                return;
            }

            ClientMessage.Flash(this, "Announcement posted.");
            Response.Redirect("~/Announcements.aspx");
        }

        protected void gvAnnouncements_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            int id = Convert.ToInt32(gvAnnouncements.DataKeys[e.RowIndex]["Id"]);

            OperationResult result = _announcements.Delete(id);
            if (!result.Success)
                ClientMessage.Show(this, result.Message);

            BindGrid();
        }
    }
}
