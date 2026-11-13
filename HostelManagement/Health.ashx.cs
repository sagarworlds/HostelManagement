using System.Web;

namespace HostelManagement
{
    /// <summary>
    /// The address a monitor (or a load balancer) calls to ask whether the site can do its job: 200 and "Healthy" when it
    /// can reach a database that has everything this version needs, 503 and "Unhealthy" when it cannot. Open to anyone and
    /// deliberately terse: the reason is in the application log, not on a page a stranger can read.
    /// </summary>
    /// <remarks>A plain handler rather than a page: no session, no cookies, no master page, so a monitor that calls it every few seconds creates nothing on the server.</remarks>
    public class Health : IHttpHandler
    {
        /// <summary>The handler keeps no state, so one instance can serve every request.</summary>
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            bool healthy = AppServices.Health().IsHealthy();

            HttpResponse response = context.Response;
            // Without this IIS replaces a 503 with its own page, and a monitor would read that instead.
            response.TrySkipIisCustomErrors = true;
            response.StatusCode = healthy ? 200 : 503;
            response.ContentType = "text/plain";
            // A monitor must see the state of the site now, not whatever a proxy remembers.
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();
            response.Write(healthy ? "Healthy" : "Unhealthy");
        }
    }
}
