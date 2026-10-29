using System.Web.Configuration;

namespace HostelManagement.CompileChecks
{
    /// <summary>
    /// Compile-only. Each line fails the build if a Web.config attribute the application relies on is not a property of
    /// the .NET Framework 4.8 configuration section it appears in. A misspelt or unsupported attribute would otherwise
    /// only show up when the site starts, as "Unrecognized attribute".
    /// </summary>
    internal static class WebConfigAttributesExist
    {
        internal static void Check()
        {
            // <httpCookies httpOnlyCookies requireSSL sameSite>
            var cookies = new HttpCookiesSection();
            bool httpOnly = cookies.HttpOnlyCookies;
            bool requireSsl = cookies.RequireSSL;
            System.Web.SameSiteMode sameSite = cookies.SameSite;

            // <sessionState mode cookieless timeout regenerateExpiredSessionId>
            var session = new SessionStateSection();
            System.Web.SessionState.SessionStateMode mode = session.Mode;
            System.Web.HttpCookieMode cookieless = session.Cookieless;
            System.TimeSpan timeout = session.Timeout;
            bool regenerate = session.RegenerateExpiredSessionId;

            // <httpRuntime targetFramework enableVersionHeader>
            var runtime = new HttpRuntimeSection();
            string targetFramework = runtime.TargetFramework.ToString();
            bool versionHeader = runtime.EnableVersionHeader;

            // <customErrors mode defaultRedirect redirectMode>
            var errors = new CustomErrorsSection();
            CustomErrorsMode errorsMode = errors.Mode;
            string defaultRedirect = errors.DefaultRedirect;
            CustomErrorsRedirectMode redirectMode = errors.RedirectMode;

            GC.KeepAlive(httpOnly, requireSsl, sameSite, mode, cookieless, timeout, regenerate, targetFramework, versionHeader,
                errorsMode, defaultRedirect, redirectMode);
        }

        private static class GC
        {
            // Only here so the locals above count as used.
            internal static void KeepAlive(params object[] values) { }
        }
    }
}
