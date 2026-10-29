namespace HostelManagement
{
    /// <summary>
    /// Composition root of the web application and the only place that names concrete repository and
    /// logger types. Pages ask for an interface here, so swapping an implementation (or faking one in a
    /// test) does not touch page code.
    /// </summary>
    public static class AppServices
    {
        public static ILogger Logger()
        {
            return new FileLogger();
        }

        public static IUserRepository Users()
        {
            return new UserDB(Logger());
        }
    }
}
