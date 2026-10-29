using System;
using System.Net.Mail;

namespace HostelManagement
{
    /// <summary>
    /// Mail server and public address settings for emailed password reset, read from the git-ignored
    /// appSettings.local.config. The feature is only offered when all of them are present and sensible, so a half
    /// configured server never produces broken links or failed sends.
    /// </summary>
    public class SmtpSettings
    {
        public string Host { get; private set; }
        public int Port { get; private set; }
        public string UserName { get; private set; }
        public string Password { get; private set; }
        public bool EnableSsl { get; private set; }
        public string FromAddress { get; private set; }
        public string FromName { get; private set; }

        /// <summary>Absolute address of the site root, used to build links in e-mails. Never taken from the request.</summary>
        public string BaseUrl { get; private set; }

        /// <summary>True when e-mail can actually be sent and links can be built.</summary>
        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(Host) && IsValidAddress(FromAddress) && IsValidBaseUrl(BaseUrl); }
        }

        /// <param name="get">Looks up an app setting by name; returns null or empty when it is absent.</param>
        public static SmtpSettings From(Func<string, string> get)
        {
            int port;
            bool enableSsl;

            return new SmtpSettings
            {
                Host = Clean(get("Smtp.Host")),
                Port = int.TryParse(get("Smtp.Port"), out port) && port > 0 && port <= 65535 ? port : 587,
                UserName = Clean(get("Smtp.UserName")),
                Password = get("Smtp.Password") ?? string.Empty,
                // Encrypted unless explicitly turned off (some internal relays have no TLS).
                EnableSsl = !bool.TryParse(get("Smtp.EnableSsl"), out enableSsl) || enableSsl,
                FromAddress = Clean(get("Smtp.FromAddress")),
                FromName = Clean(get("Smtp.FromName")) ?? "HostelHub",
                BaseUrl = (Clean(get("App.BaseUrl")) ?? string.Empty).TrimEnd('/')
            };
        }

        /// <summary>
        /// Links carry a secret, so they must not go over plain HTTP to another machine: https, or a loopback address
        /// for development.
        /// </summary>
        public static bool IsValidBaseUrl(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
        }

        public static bool IsValidAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            try
            {
                return new MailAddress(address).Address == address.Trim();
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
