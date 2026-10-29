using System;
using System.Net;
using System.Net.Mail;

namespace HostelManagement
{
    /// <summary>Sends mail through the SMTP server named in <see cref="SmtpSettings"/>.</summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpSettings _settings;

        public SmtpEmailSender(SmtpSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (!settings.IsConfigured)
                throw new ArgumentException("The SMTP settings are incomplete.", "settings");

            _settings = settings;
        }

        public void Send(string toAddress, string subject, string body)
        {
            try
            {
                using (var message = new MailMessage())
                using (var client = new SmtpClient(_settings.Host, _settings.Port))
                {
                    message.From = new MailAddress(_settings.FromAddress, _settings.FromName);
                    message.To.Add(new MailAddress(toAddress));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = false;

                    client.EnableSsl = _settings.EnableSsl;
                    client.Timeout = 15000;
                    if (!string.IsNullOrEmpty(_settings.UserName))
                        client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);

                    client.Send(message);
                }
            }
            catch (SmtpException ex)
            {
                throw new EmailDeliveryException("The mail server did not accept the message.", ex);
            }
            catch (FormatException ex)
            {
                throw new EmailDeliveryException("The recipient address is not valid.", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new EmailDeliveryException("The mail client could not send the message.", ex);
            }
        }
    }
}
