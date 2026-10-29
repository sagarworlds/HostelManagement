using System.Net;

namespace HostelManagement
{
    public static class HtmlText
    {
        /// <summary>
        /// Turns plain text typed by a user into HTML that is safe to place in a page: every character that means
        /// something to HTML is encoded, and line breaks become &lt;br /&gt; so paragraphs survive.
        /// </summary>
        public static string FromPlain(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return WebUtility.HtmlEncode(text)
                .Replace("\r\n", "<br />")
                .Replace("\n", "<br />")
                .Replace("\r", "<br />");
        }
    }
}
