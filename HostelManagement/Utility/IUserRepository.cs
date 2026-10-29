using System.Collections.Generic;

namespace HostelManagement
{
    /// <summary>Persistence of administrator and student accounts.</summary>
    /// <remarks>
    /// Reads throw <see cref="DataAccessException"/> when the database fails, so an outage is never
    /// mistaken for "no data". Writes return an <see cref="OperationResult"/> with a user-safe message.
    /// </remarks>
    public interface IUserRepository
    {
        /// <summary>Creates an account. The plain password in <paramref name="user"/> is hashed before it is stored.</summary>
        /// <returns>Failure with an "already exists" message when the e-mail address is taken.</returns>
        OperationResult AddUser(User user);

        /// <summary>
        /// Updates name and e-mail of the account with <c>user.Id</c>. The password and the account type are
        /// never changed; <c>user.UserType</c> must match the stored type or nothing is updated.
        /// </summary>
        OperationResult UpdateUser(User user);

        /// <summary>Updates the name and mobile number of the account; e-mail, type and password are left alone.</summary>
        OperationResult UpdateProfile(User user);

        /// <summary>
        /// Lets a signed-in user choose a new password. The current password must be correct; on success the
        /// "must change password" flag is cleared.
        /// </summary>
        OperationResult ChangePassword(int userId, string currentPassword, string newPassword);

        /// <summary>
        /// Sets a student's password to <paramref name="temporaryPassword"/> and requires them to replace it at
        /// their next sign-in. Only student accounts can be reset this way.
        /// </summary>
        OperationResult ResetStudentPassword(int studentId, string temporaryPassword);

        /// <summary>Checks credentials.</summary>
        /// <returns>The account on success; null when the e-mail is unknown or the password is wrong.</returns>
        /// <exception cref="DataAccessException">The database failed.</exception>
        User Authenticate(string email, string password);

        /// <summary>True when another account already uses <paramref name="email"/>.</summary>
        /// <param name="excludeUserId">Account to ignore (the one being edited); 0 to consider every account.</param>
        /// <exception cref="DataAccessException">The database failed.</exception>
        bool EmailExists(string email, int excludeUserId);

        /// <summary>Lists accounts of one type, ordered by name.</summary>
        /// <param name="userType">A value from <see cref="UserTypes"/>.</param>
        /// <exception cref="DataAccessException">The database failed.</exception>
        List<User> GetUsersByType(string userType);

        /// <returns>The account, or null when it does not exist.</returns>
        /// <exception cref="DataAccessException">The database failed.</exception>
        User GetUserById(int id);

        /// <summary>
        /// Deletes a student together with their room requests, and frees the room they hold, in one transaction.
        /// </summary>
        OperationResult DeleteStudent(int id);

        /// <summary>
        /// Deletes an administrator. Refused for the signed-in administrator and for the last remaining one.
        /// </summary>
        /// <param name="id">Account to delete.</param>
        /// <param name="currentAdminId">The signed-in administrator, who may not delete themselves.</param>
        OperationResult DeleteAdmin(int id, int currentAdminId);
    }
}
