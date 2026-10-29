using System;
using System.Web.UI;

namespace HostelManagement
{
    public partial class ForgotPassword : System.Web.UI.Page
    {
        private readonly PasswordResetService _reset = AppServices.PasswordReset();

        protected void Page_Load(object sender, EventArgs e)
        {
            btnSend.Click += BtnSend_Click;
            if (!IsPostBack && !_reset.IsEnabled)
                ShowOnly(pnlUnavailable);
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            try
            {
                switch (_reset.RequestReset(inputEmail.Text.Trim(), Request.UserHostAddress))
                {
                    case ResetRequestOutcome.Accepted:
                        // Same answer whether or not the address has an account.
                        ShowOnly(pnlSent);
                        break;
                    case ResetRequestOutcome.Throttled:
                        ClientMessage.Show(this, "Too many requests. Please wait a while before asking for another link.");
                        break;
                    default:
                        ShowOnly(pnlUnavailable);
                        break;
                }
            }
            catch (DataAccessException)
            {
                // The repository has already logged the cause.
                ClientMessage.Show(this, ClientMessage.ServiceUnavailable);
            }
        }

        private void ShowOnly(System.Web.UI.WebControls.Panel panel)
        {
            pnlRequest.Visible = panel == pnlRequest;
            pnlSent.Visible = panel == pnlSent;
            pnlUnavailable.Visible = panel == pnlUnavailable;
        }
    }
}
