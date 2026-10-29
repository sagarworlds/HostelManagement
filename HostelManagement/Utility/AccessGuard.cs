using System;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;

namespace HostelManagement
{
    /// <summary>
    /// Page access rules shared by the role master pages: the visitor must be signed in with the right account
    /// type (and hold the cookie issued at sign-in, see <see cref="LoginBinding"/>), and a user who has to choose a
    /// new password (after an administrator reset it) can reach nothing but the profile page until they do.
    /// Signed-in pages are also kept out of browser caches and their view state is tied to the session.
    /// </summary>
    public static class AccessGuard
    {
        public const string ProfileUrl = "~/MyProfile.aspx";
        public const string MustChangePasswordKey = "MustChangePassword";

        private const string LoginUrl = "~/Login.aspx";

        /// <summary>Redirects away from the current page unless the visitor may view it. Call from the master's Init.</summary>
        /// <param name="page">The content page being requested.</param>
        /// <param name="requiredUserType">A value from <see cref="UserTypes"/>.</param>
        public static void Require(Page page, string requiredUserType)
        {
            // Without this the browser's Back button can show a signed-in page after sign-out.
            DisableCaching(page);

            HttpSessionState session = page.Session;
            bool hasIdentity = session["UserId"] != null;
            bool signedInAsRequiredType = hasIdentity
                && string.Equals(Convert.ToString(session["UserType"]), requiredUserType, StringComparison.Ordinal);

            if (signedInAsRequiredType && !LoginBinding.IsValid(page))
            {
                // A session that holds an identity but arrives without the sign-in cookie is not the browser that
                // signed in: most likely a planted or stolen session id.
                AppServices.Logger().Warn("Rejected a session without its sign-in cookie for user " + session["UserId"] +
                    " from " + page.Request.UserHostAddress + ".");
                session.Clear();
                LoginBinding.Clear(page);
                signedInAsRequiredType = false;
            }

            if (!signedInAsRequiredType)
            {
                page.Response.Redirect(LoginUrl);
                return;
            }

            // Mixes the session id into view-state validation, so a post forged by another site is rejected.
            page.ViewStateUserKey = session.SessionID;

            if (MustChangePassword(session) && !IsProfilePage(page))
                page.Response.Redirect(ProfileUrl);
        }

        private static void DisableCaching(Page page)
        {
            HttpCachePolicy cache = page.Response.Cache;
            cache.SetCacheability(HttpCacheability.NoCache);
            cache.SetNoStore();
            cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
        }

        public static bool MustChangePassword(HttpSessionState session)
        {
            object value = session[MustChangePasswordKey];
            return value is bool && (bool)value;
        }

        private static bool IsProfilePage(Page page)
        {
            return string.Equals(page.Request.AppRelativeCurrentExecutionFilePath, ProfileUrl, StringComparison.OrdinalIgnoreCase);
        }
    }
}
