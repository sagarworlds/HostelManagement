using System;

namespace HostelManagement
{
    /// <summary>An e-mail could not be handed to the mail server (unreachable, rejected credentials, bad address).</summary>
    public class EmailDeliveryException : Exception
    {
        public EmailDeliveryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
