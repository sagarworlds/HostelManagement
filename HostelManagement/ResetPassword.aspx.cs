using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class ResetPassword : System.Web.UI.Page
    {
        private readonly PasswordResetService _reset = AppServices.PasswordReset();

        /// <summary>The secret from the e-mailed link. It is only ever read, never written back into the page.</summary>
        private string Token
        {
            get { return Request.QueryString["token"]; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            btnReset.Click += BtnReset_Click;
            if (IsPostBack)
                return;

            if (!_reset.IsEnabled || !_reset.IsTokenValid(Token))
                ShowInvalid();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            OperationResult result = _reset.Complete(Token, inputPassword.Text);
            if (result.Success)
            {
                ClientMessage.Show(this, "Your password has been changed. Please sign in.", "~/Login.aspx");
                return;
            }

            if (result.Message == PasswordResetService.InvalidLinkMessage)
                ShowInvalid();
            else
                ClientMessage.Show(this, result.Message);
        }

        private void ShowInvalid()
        {
            pnlForm.Visible = false;
            pnlInvalid.Visible = true;
        }
    }
}
