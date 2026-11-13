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
        /// still leaves a trace in the application log.
        /// </summary>
        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            if (exception == null)
                return;

            new FileLogger().Error("Unhandled exception for " + Request.RawUrl, exception);
        }
    }
}
