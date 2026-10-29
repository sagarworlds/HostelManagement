using System;
using System.Web;
using System.Web.UI;

namespace HostelManagement
{
    /// <summary>
    /// Ties a signed-in session to a second, random cookie issued at sign-in.
    /// <para>
    /// Session fixation: an attacker who can plant a session id in a victim's browser waits for the victim to sign in
    /// and then uses the same id. ASP.NET cannot change the id of a session mid-request, so instead the session is
    /// only trusted together with a secret that is created at sign-in and is held only by the browser that signed in.
    /// The attacker has the session id but not that cookie, so the planted session is useless to them.
    /// </para>
    /// </summary>
    public static class LoginBinding
    {
        public const string CookieName = "HostelHubAuth";
        private const string SessionKey = "LoginBindingToken";

        /// <summary>Call right after the identity has been stored in the session.</summary>
        public static void Establish(Page page)
        {
            string token = SecureToken.Create();
            page.Session[SessionKey] = token;

            // No Expires: the cookie lives as long as the browser session, like the session cookie it protects.
            var cookie = new HttpCookie(CookieName, token)
            {
                HttpOnly = true,
                Secure = page.Request.IsSecureConnection,
                SameSite = SameSiteMode.Lax
            };
            page.Response.Cookies.Add(cookie);
        }

        /// <returns>True when the browser presents the cookie that goes with this session.</returns>
        public static bool IsValid(Page page)
        {
            string expected = page.Session[SessionKey] as string;
            HttpCookie cookie = page.Request.Cookies[CookieName];

            return expected != null && cookie != null && SecureToken.FixedTimeEquals(expected, cookie.Value);
        }

        /// <summary>Call at sign-out (and when a binding fails) so the cookie does not linger.</summary>
        public static void Clear(Page page)
        {
            page.Session.Remove(SessionKey);

            var expired = new HttpCookie(CookieName, string.Empty)
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddYears(-1)
            };
            page.Response.Cookies.Add(expired);
        }
    }
}
