using System;
using System.Web;

namespace HostelManagement
{
    public class Global : HttpApplication
    {
        /// <summary>
        /// Runs once when the application starts: checks the deployment (see <see cref="StartupChecks"/>) and writes what it
        /// finds to the log, so a database that was not upgraded shows up in the first lines of the log.
        /// </summary>
        protected void Application_Start(object sender, EventArgs e)
        {
            try
            {
                AppServices.Startup().Run();
            }
            catch (Exception ex)
            {
                // A diagnostic must never be the reason the site does not start. The failure is logged, not hidden.
                new FileLogger().Error("The start-up checks failed unexpectedly", ex);
            }
        }

        /// <summary>
        /// Last-resort logging for exceptions no page handled, so a failure that ends in an error page
        /// still leaves a trace in the application log. Input that ASP.NET refused is the exception: that is
        /// not a failure of ours, and the visitor is told what happened instead.
        /// </summary>
        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            if (exception == null)
                return;

            if (IsRefusedInput(exception))
            {
                // What was typed is not logged: it is the visitor's own text and can be anything. That it happened, and
                // where, is. A warning, not an error: nothing is wrong with the application.
                new FileLogger().Warn("Request validation refused input sent to " + Request.Path + ".");
                Server.ClearError();
                Response.Redirect("~/Error.aspx?code=400", false);
                CompleteRequest();
                return;
            }

            new FileLogger().Error("Unhandled exception for " + Request.RawUrl, exception);
        }

        /// <summary>
        /// ASP.NET request validation throws HttpRequestValidationException for a form field, query string value or
        /// cookie that looks like markup. Depending on where it surfaces it may be wrapped in another exception.
        /// </summary>
        private static bool IsRefusedInput(Exception exception)
        {
            for (Exception inner = exception; inner != null; inner = inner.InnerException)
            {
                if (inner is HttpRequestValidationException)
                    return true;
            }

            return false;
        }
    }
}
