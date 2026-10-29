using System;
using System.Data.SqlClient;
using HostelManagement.Utility;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// Arranges and inspects rows with plain SQL, deliberately not through the repositories under test, so a defect in
    /// (say) UserDB cannot make the tests of RoomDB pass or fail by accident.
    /// </summary>
    public sealed class TestData
    {
        /// <summary>Few iterations keep fixture accounts fast; the repositories' own hashing still uses the full count.</summary>
        private const int FixtureIterations = 1000;

        private static int _counter;

        private readonly string _connectionString;

        public TestData(string connectionString)
        {
            _connectionString = connectionString;
        }

        public int AddUser(string userType, string email = null, string firstName = "Test", string lastName = "Person",
            string password = "correct horse battery", string rawPasswordHash = null, bool mustChangePassword = false)
        {
            string unique = Next();
            return Scalar<int>(
                "INSERT INTO dbo.tbl_User (Email, [Password], UserType, FirstName, LastName, MobileNo, MustChangePassword, CreatedOn, ModifiedOn) " +
                "OUTPUT INSERTED.Id VALUES (@Email, @Password, @UserType, @FirstName, @LastName, N'0000', @MustChange, GETDATE(), GETDATE());",
                new SqlParameter("@Email", email ?? ("user" + unique + "@example.test")),
                new SqlParameter("@Password", rawPasswordHash ?? PasswordUtility.HashPassword(password, FixtureIterations)),
                new SqlParameter("@UserType", userType),
                new SqlParameter("@FirstName", firstName),
                new SqlParameter("@LastName", lastName),
                new SqlParameter("@MustChange", mustChangePassword));
        }

        public int AddStudent(string email = null, string firstName = "Sam", string lastName = "Student")
        {
            return AddUser(UserTypes.Student, email, firstName, lastName);
        }

        public int AddAdmin(string email = null, string firstName = "Ada", string lastName = "Admin")
        {
            return AddUser(UserTypes.Admin, email, firstName, lastName);
        }

        public int AddRoom(string name = null, int rent = 100, bool available = true, string description = "A room")
        {
            return Scalar<int>(
                "INSERT INTO dbo.tbl_Room (RoomName, Rent, [Description], Availability, CreatedOn, ModifiedOn) " +
                "OUTPUT INSERTED.Id VALUES (@Name, @Rent, @Description, @Available, GETDATE(), GETDATE());",
                new SqlParameter("@Name", name ?? ("Room " + Next())),
                new SqlParameter("@Rent", rent),
                new SqlParameter("@Description", description),
                new SqlParameter("@Available", available));
        }

        /// <summary>Inserts a request in any state. An Approved one does not touch the room's Availability; arrange that separately.</summary>
        public int AddRequest(int userId, string status = RequestStatus.Pending, int? approvedRoomId = null,
            int? preferredRoomId = null, DateTime? requestDate = null)
        {
            return Scalar<int>(
                "INSERT INTO dbo.tbl_RequestRoom (UserId, ApprovedRoomId, PreferredRoomId, [Status], CreatedOn, RequestDate) " +
                "OUTPUT INSERTED.Id VALUES (@UserId, @Approved, @Preferred, @Status, GETDATE(), @RequestDate);",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Approved", (object)approvedRoomId ?? DBNull.Value),
                new SqlParameter("@Preferred", (object)preferredRoomId ?? DBNull.Value),
                new SqlParameter("@Status", status),
                new SqlParameter("@RequestDate", requestDate ?? DateTime.Now));
        }

        /// <summary>A room that a student holds: available = 0 and an Approved request pointing at it.</summary>
        public int AllocateRoom(int roomId, int studentId)
        {
            Execute("UPDATE dbo.tbl_Room SET Availability = 0 WHERE Id = @Id;", new SqlParameter("@Id", roomId));
            return AddRequest(studentId, RequestStatus.Approved, roomId);
        }

        public string StatusOf(int requestId)
        {
            return Scalar<string>("SELECT [Status] FROM dbo.tbl_RequestRoom WHERE Id = @Id;", new SqlParameter("@Id", requestId));
        }

        public bool IsAvailable(int roomId)
        {
            return Scalar<bool>("SELECT Availability FROM dbo.tbl_Room WHERE Id = @Id;", new SqlParameter("@Id", roomId));
        }

        public int Count(string table)
        {
            return Scalar<int>("SELECT COUNT(1) FROM " + table + ";");
        }

        public void Execute(string sql, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        public T Scalar<T>(string sql, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? default(T) : (T)value;
            }
        }

        private static string Next()
        {
            return System.Threading.Interlocked.Increment(ref _counter).ToString();
        }
    }
}
