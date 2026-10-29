namespace HostelManagement
{
    /// <summary>
    /// Outcome of a write operation. A failure carries a message that is safe to show to the user;
    /// technical detail is logged where the failure happened, never put in the message.
    /// </summary>
    public class OperationResult
    {
        private OperationResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        /// <summary>True when the operation did what was asked.</summary>
        public bool Success { get; private set; }

        /// <summary>User-facing explanation; empty on success.</summary>
        public string Message { get; private set; }

        public static OperationResult Ok()
        {
            return new OperationResult(true, string.Empty);
        }

        /// <param name="message">What went wrong and, where possible, what the user can do about it.</param>
        public static OperationResult Fail(string message)
        {
            return new OperationResult(false, message);
        }
    }
}
