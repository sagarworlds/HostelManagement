using System;
using System.Configuration;
using HostelManagement.Utility;

namespace HostelManagement
{
    /// <summary>
    /// Composition root of the web application and the only place that names concrete repository and
    /// logger types. Pages ask for an interface here, so swapping an implementation (or faking one in a
    /// test) does not touch page code.
    /// </summary>
    public static class AppServices
    {
        // Failed sign-ins must be counted across requests, so unlike the repositories this one instance is shared.
        private static readonly LoginAttemptPolicy SharedLoginAttempts = new LoginAttemptPolicy(() => DateTime.UtcNow);

        public static LoginAttemptPolicy LoginAttempts()
        {
            return SharedLoginAttempts;
        }

        // Likewise the reset-request limits must outlive a single request.
        private static readonly RateLimiter ResetRequestsPerEmail = new RateLimiter(
            PasswordResetService.MaximumRequestsPerEmail, PasswordResetService.RequestWindow, () => DateTime.UtcNow);
        private static readonly RateLimiter ResetRequestsPerAddress = new RateLimiter(
            PasswordResetService.MaximumRequestsPerAddress, PasswordResetService.RequestWindow, () => DateTime.UtcNow);

        /// <summary>
        /// The emailed password reset. When the mail settings are missing or unusable it comes back disabled (no sender)
        /// instead of failing, so the rest of the application is unaffected.
        /// </summary>
        public static PasswordResetService PasswordReset()
        {
            SmtpSettings settings = SmtpSettings.From(key => ConfigurationManager.AppSettings[key]);
            IEmailSender sender = settings.IsConfigured ? new SmtpEmailSender(settings) : null;

            return new PasswordResetService(Users(), new PasswordResetDB(Logger()), sender, Logger(),
                () => DateTime.UtcNow, settings.BaseUrl, ResetRequestsPerEmail, ResetRequestsPerAddress);
        }

        public static ILogger Logger()
        {
            return new FileLogger();
        }

        public static IUserRepository Users()
        {
            return new UserDB(Logger());
        }

        public static IRoomRepository Rooms()
        {
            return new RoomDB(Logger());
        }

        public static IAnnouncementRepository Announcements()
        {
            return new AnnouncementDB(Logger());
        }

        public static IDashboardRepository Dashboard()
        {
            return new DashboardDB(Logger());
        }

        public static IRequestRoomRepository Requests()
        {
            return new RequestRoomDB(Logger());
        }
    }
}
