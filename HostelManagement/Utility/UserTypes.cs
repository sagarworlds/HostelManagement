using System;

namespace HostelManagement
{
    /// <summary>Values stored in tbl_User.UserType.</summary>
    public static class UserTypes
    {
        public const string Admin = "Admin";
        public const string Student = "Student";

        /// <summary>
        /// Maps untrusted input (typically a query-string value) to a known user type. Anything that is not
        /// "Student" falls back to <see cref="Admin"/>, so the value is always safe to use in logic and queries.
        /// </summary>
        public static string Normalize(string value)
        {
            return string.Equals(value, Student, StringComparison.OrdinalIgnoreCase) ? Student : Admin;
        }
    }
}
