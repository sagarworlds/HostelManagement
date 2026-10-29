namespace HostelManagement
{
    /// <summary>Sends plain-text e-mail. Behind an interface so the reset flow can be tested without a mail server.</summary>
    public interface IEmailSender
    {
        /// <exception cref="EmailDeliveryException">The message could not be handed to the mail server.</exception>
        void Send(string toAddress, string subject, string body);
    }
}
