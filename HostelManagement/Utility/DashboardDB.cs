using System;
using System.Data;
using System.Data.SqlClient;
using HostelManagement.Utility;

namespace HostelManagement
{
    /// <summary>SQL Server implementation of <see cref="IDashboardRepository"/>.</summary>
    public class DashboardDB : RepositoryBase, IDashboardRepository
    {
        public DashboardDB(ILogger logger) : base(logger)
        {
        }

        public DashboardSummary GetSummary()
        {
            // Occupied rooms are counted from allocations (one room can appear once even if history repeats it).
            const string sql =
                "SELECT " +
                "(SELECT COUNT(1) FROM dbo.tbl_Room) AS TotalRooms, " +
                "(SELECT COUNT(1) FROM dbo.tbl_Room WHERE Availability = 1) AS AvailableRooms, " +
                "(SELECT COUNT(DISTINCT ApprovedRoomId) FROM dbo.tbl_RequestRoom WHERE Status = @Approved) AS OccupiedRooms, " +
                "(SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE Status = @Pending) AS PendingRequests, " +
                "(SELECT COUNT(1) FROM dbo.tbl_User WHERE UserType = @Student) AS Students, " +
                "(SELECT COUNT(1) FROM dbo.tbl_User WHERE UserType = @Admin) AS Admins;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(
                        Param("@Approved", RequestStatus.Approved),
                        Param("@Pending", RequestStatus.Pending),
                        Param("@Student", UserTypes.Student),
                        Param("@Admin", UserTypes.Admin)));

                    DataRow row = table.Rows[0];
                    return new DashboardSummary
                    {
                        TotalRooms = Convert.ToInt32(row["TotalRooms"]),
                        AvailableRooms = Convert.ToInt32(row["AvailableRooms"]),
                        OccupiedRooms = Convert.ToInt32(row["OccupiedRooms"]),
                        PendingRequests = Convert.ToInt32(row["PendingRequests"]),
                        Students = Convert.ToInt32(row["Students"]),
                        Admins = Convert.ToInt32(row["Admins"])
                    };
                }
            }
            catch (SqlException ex)
            {
                throw Failure("load dashboard summary", ex);
            }
        }
    }
}
