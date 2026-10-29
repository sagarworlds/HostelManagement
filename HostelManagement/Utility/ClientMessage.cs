using System.Web.Script.Serialization;
using System.Web.UI;

namespace HostelManagement
{
    /// <summary>
    /// Shows messages in the shared alert modal (Controls/AlertModal.ascx, included by every master page).
    /// Messages are passed to JavaScript as a serialised string literal, never concatenated into script,
    /// so text containing quotes or markup cannot break out of the script block.
    /// </summary>
    public static class ClientMessage
    {
        private const string ScriptKey = "ClientMessage";
        private const string FlashSessionKey = "FlashMessage";

        /// <summary>Shown when a read fails because the database is unavailable (the cause is already logged).</summary>
        public const string ServiceUnavailable = "The service is temporarily unavailable. Please try again in a few minutes.";

        /// <summary>Shows <paramref name="message"/> when the current page finishes loading.</summary>
        public static void Show(Page page, string message)
        {
            Show(page, message, null);
        }

        /// <param name="redirectUrlOnClose">App-relative URL (e.g. "~/Login.aspx") to open when the user dismisses the dialog; null to stay.</param>
        public static void Show(Page page, string message, string redirectUrlOnClose)
        {
            string script = "showModalmsg(" + ToJavaScriptLiteral(message);
            if (redirectUrlOnClose != null)
                script += ", " + ToJavaScriptLiteral(page.ResolveUrl(redirectUrlOnClose));
            script += ");";

            page.ClientScript.RegisterStartupScript(page.GetType(), ScriptKey, script, true);
        }

        /// <summary>
        /// Keeps a message across a redirect: store it before Response.Redirect and the next page shows it once.
        /// </summary>
        public static void Flash(Page page, string message)
        {
            page.Session[FlashSessionKey] = message;
        }

        /// <summary>Shows and clears a message stored by <see cref="Flash"/>; called by the alert control on each load.</summary>
        public static void ShowPendingFlash(Page page)
        {
            string message = page.Session[FlashSessionKey] as string;
            if (message == null)
                return;

            page.Session.Remove(FlashSessionKey);
            Show(page, message);
        }

        private static string ToJavaScriptLiteral(string value)
        {
            // JavaScriptSerializer escapes quotes, backslashes and control characters; "<" and ">" are escaped
            // too so a message can never contain a literal closing script tag.
            return new JavaScriptSerializer()
                .Serialize(value ?? string.Empty)
                .Replace("<", "\\u003c")
                .Replace(">", "\\u003e");
        }
    }
}
