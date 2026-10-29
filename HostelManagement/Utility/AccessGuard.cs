using System;
using System.Web.SessionState;
using System.Web.UI;

namespace HostelManagement
{
    /// <summary>
    /// Page access rules shared by the role master pages: the visitor must be signed in with the right account
    /// type, and a user who has to choose a new password (after an administrator reset it) can reach nothing
    /// but the profile page until they do.
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
            HttpSessionState session = page.Session;
            bool signedInAsRequiredType = session["UserId"] != null
                && string.Equals(Convert.ToString(session["UserType"]), requiredUserType, StringComparison.Ordinal);

            if (!signedInAsRequiredType)
            {
                page.Response.Redirect(LoginUrl);
                return;
            }

            if (MustChangePassword(session) && !IsProfilePage(page))
                page.Response.Redirect(ProfileUrl);
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
