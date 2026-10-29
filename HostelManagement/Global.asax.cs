using System;
using System.Web;

namespace HostelManagement
{
    public class Global : HttpApplication
    {
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
