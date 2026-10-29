using System;

namespace HostelManagement
{
    /// <summary>
    /// Single-use, expiring password reset links. Only the SHA-256 of a link's token is ever stored, so a copy of the
    /// database cannot be turned into working links. Reads throw <see cref="DataAccessException"/>; writes return an
    /// <see cref="OperationResult"/>.
    /// </summary>
    public interface IPasswordResetRepository
    {
        /// <summary>Stores a new link for the user and retires any earlier unused ones, so only the latest e-mail works.</summary>
        OperationResult CreateToken(int userId, string tokenHash, DateTime createdOn, DateTime expiresOn);

        /// <summary>True when the token exists, has not been used and has not expired.</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        bool IsTokenValid(string tokenHash, DateTime now);

        /// <summary>
        /// In one transaction: checks the token, sets the user's new password hash (clearing "must change password"),
        /// and retires this and every other unused link of that user. Fails, changing nothing, if the token is unknown,
        /// used or expired.
        /// </summary>
        OperationResult CompleteReset(string tokenHash, string newPasswordHash, DateTime now);
    }
}
