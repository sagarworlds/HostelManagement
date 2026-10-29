using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace HostelManagement.Utility
{
    /// <summary>SQL Server implementation of <see cref="IRequestRoomRepository"/>.</summary>
    public class RequestRoomDB : RepositoryBase, IRequestRoomRepository
    {
        private const string AlreadyProcessedMessage = "This request has already been processed.";

        public RequestRoomDB(ILogger logger) : base(logger)
        {
        }

        public OperationResult AddRoomRequest(RequestRoom request)
        {
            // The "no active request yet" test is part of the insert, and UPDLOCK/HOLDLOCK keep the range locked, so
            // two clicks (or two browser tabs) at the same moment cannot both create a request.
            const string sql =
                "INSERT INTO dbo.tbl_RequestRoom (UserId, Status, CreatedOn, RequestDate, PreferredRoomId) " +
                "SELECT @UserId, @Pending, @CreatedOn, @RequestDate, @PreferredRoomId " +
                "WHERE NOT EXISTS (SELECT 1 FROM dbo.tbl_RequestRoom WITH (UPDLOCK, HOLDLOCK) " +
                "                  WHERE UserId = @UserId AND Status IN (@Pending, @Approved));";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@UserId", request.UserId),
                        Param("@Pending", RequestStatus.Pending),
                        Param("@Approved", RequestStatus.Approved),
                        Param("@CreatedOn", request.CreatedOn),
                        Param("@RequestDate", request.RequestDate),
                        Param("@PreferredRoomId", SqlDbType.Int, request.PreferredRoomId > 0 ? (object)request.PreferredRoomId : null)));

                    return rows == 1
                        ? OperationResult.Ok()
                        : OperationResult.Fail("You already have a pending request or an allocated room.");
                }
            }
            catch (SqlException ex)
            {
                // Either the account was deleted while the session was open, or the room asked for no longer exists.
                if (IsForeignKeyViolation(ex))
                    return OperationResult.Fail("Your request could not be saved because your account or the selected room no longer exists.");

                return FailureResult("add room request for user " + request.UserId, ex);
            }
        }

        public OperationResult CancelRequest(int requestId, int userId)
        {
            const string sql =
                "UPDATE dbo.tbl_RequestRoom SET Status = @Cancelled, ModifiedOn = @Now, ModifiedBy = @UserId " +
                "WHERE Id = @Id AND UserId = @UserId AND Status = @Pending;";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@Cancelled", RequestStatus.Cancelled),
                        Param("@Now", DateTime.Now),
                        Param("@UserId", userId),
                        Param("@Id", requestId),
                        Param("@Pending", RequestStatus.Pending)));

                    return rows == 1
                        ? OperationResult.Ok()
                        : OperationResult.Fail("Only your own pending requests can be cancelled.");
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("cancel request " + requestId, ex);
            }
        }

        public OperationResult ApproveRequest(int requestId, int roomId, int adminId)
        {
            DateTime now = DateTime.Now;

            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();

                    int requestRows = db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_RequestRoom SET Status = @Approved, ApprovedRoomId = @RoomId, ModifiedOn = @Now, " +
                        "ModifiedBy = @AdminId, ResponseDate = @Now WHERE Id = @RequestId AND Status = @Pending;",
                        Params(
                            Param("@Approved", RequestStatus.Approved),
                            Param("@RoomId", roomId),
                            Param("@Now", now),
                            Param("@AdminId", adminId),
                            Param("@RequestId", requestId),
                            Param("@Pending", RequestStatus.Pending)));
                    if (requestRows == 0)
                        return OperationResult.Fail(AlreadyProcessedMessage);

                    // Only a room that is still free can be taken. If another administrator got there first this
                    // matches no row, and leaving the using block rolls back the request update above.
                    int roomRows = db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_Room SET Availability = 0, ModifiedBy = @AdminId, ModifiedOn = @Now " +
                        "WHERE Id = @RoomId AND Availability = 1;",
                        Params(
                            Param("@AdminId", adminId),
                            Param("@Now", now),
                            Param("@RoomId", roomId)));
                    if (roomRows == 0)
                        return OperationResult.Fail("The selected room is no longer available. Choose another room.");

                    db.CommitTransaction();
                    return OperationResult.Ok();
                }
            }
            catch (SqlException ex)
            {
                if (IsForeignKeyViolation(ex))
                    return OperationResult.Fail("The selected room does not exist.");

                return FailureResult("approve request " + requestId + " for room " + roomId, ex);
            }
        }

        public OperationResult RejectRequest(int requestId, int adminId)
        {
            const string sql =
                "UPDATE dbo.tbl_RequestRoom SET Status = @Rejected, ApprovedRoomId = NULL, ModifiedOn = @Now, " +
                "ModifiedBy = @AdminId, ResponseDate = @Now WHERE Id = @Id AND Status = @Pending;";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@Rejected", RequestStatus.Rejected),
                        Param("@Now", DateTime.Now),
                        Param("@AdminId", adminId),
                        Param("@Id", requestId),
                        Param("@Pending", RequestStatus.Pending)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail(AlreadyProcessedMessage);
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("reject request " + requestId, ex);
            }
        }

        public OperationResult VacateRoom(int requestId, int adminId)
        {
            DateTime now = DateTime.Now;

            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();

                    int requestRows = db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_RequestRoom SET Status = @Vacated, ModifiedOn = @Now, ModifiedBy = @AdminId " +
                        "WHERE Id = @Id AND Status = @Approved;",
                        Params(
                            Param("@Vacated", RequestStatus.Vacated),
                            Param("@Now", now),
                            Param("@AdminId", adminId),
                            Param("@Id", requestId),
                            Param("@Approved", RequestStatus.Approved)));
                    if (requestRows == 0)
                        return OperationResult.Fail("This room is no longer allocated.");

                    // The request keeps ApprovedRoomId as history; only the room's availability is restored.
                    int roomRows = db.lfnExecuteNonQuery(
                        "UPDATE R SET R.Availability = 1, R.ModifiedBy = @AdminId, R.ModifiedOn = @Now " +
                        "FROM dbo.tbl_Room R INNER JOIN dbo.tbl_RequestRoom RR ON RR.ApprovedRoomId = R.Id " +
                        "WHERE RR.Id = @Id;",
                        Params(
                            Param("@AdminId", adminId),
                            Param("@Now", now),
                            Param("@Id", requestId)));
                    if (roomRows == 0)
                    {
                        Logger.Error("Request " + requestId + " was Approved but no room row matches its ApprovedRoomId; the vacate was rolled back.", null);
                        return OperationResult.Fail(GenericWriteFailureMessage);
                    }

                    db.CommitTransaction();
                    return OperationResult.Ok();
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("vacate room for request " + requestId, ex);
            }
        }

        public List<RequestRoom> GetRoomRequestListByUserId(int userId)
        {
            const string sql =
                "SELECT RR.Id, RR.Status, RR.RequestDate, R.RoomName, PR.RoomName AS PreferredRoomName " +
                "FROM dbo.tbl_RequestRoom RR " +
                "LEFT JOIN dbo.tbl_Room R ON R.Id = RR.ApprovedRoomId " +
                "LEFT JOIN dbo.tbl_Room PR ON PR.Id = RR.PreferredRoomId " +
                "WHERE RR.UserId = @UserId ORDER BY RR.Id DESC;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@UserId", userId)));
                    return MapRequests(table);
                }
            }
            catch (SqlException ex)
            {
                throw Failure("list requests for user " + userId, ex);
            }
        }

        public List<RequestRoom> GetRoomRequestList(string status)
        {
            // Requests waiting for a decision come first, then everything else newest first. A null status means "all".
            const string sql =
                "SELECT RR.Id, RR.Status, RR.RequestDate, R.RoomName, PR.RoomName AS PreferredRoomName, " +
                "       U.FirstName + ' ' + U.LastName AS UserName " +
                "FROM dbo.tbl_RequestRoom RR " +
                "INNER JOIN dbo.tbl_User U ON U.Id = RR.UserId " +
                "LEFT JOIN dbo.tbl_Room R ON R.Id = RR.ApprovedRoomId " +
                "LEFT JOIN dbo.tbl_Room PR ON PR.Id = RR.PreferredRoomId " +
                "WHERE (@Status IS NULL OR RR.Status = @Status) " +
                "ORDER BY CASE WHEN RR.Status = @Pending THEN 0 ELSE 1 END, RR.Id DESC;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(
                        Param("@Status", SqlDbType.NVarChar, string.IsNullOrEmpty(status) ? null : status),
                        Param("@Pending", RequestStatus.Pending)));
                    return MapRequests(table);
                }
            }
            catch (SqlException ex)
            {
                throw Failure("list room requests", ex);
            }
        }

        public RequestRoom GetRoomRequestById(int id)
        {
            const string sql =
                "SELECT RR.Id, RR.Status, RR.RequestDate, RR.PreferredRoomId, R.RoomName, PR.RoomName AS PreferredRoomName, " +
                "       U.FirstName + ' ' + U.LastName AS UserName " +
                "FROM dbo.tbl_RequestRoom RR " +
                "INNER JOIN dbo.tbl_User U ON U.Id = RR.UserId " +
                "LEFT JOIN dbo.tbl_Room R ON R.Id = RR.ApprovedRoomId " +
                "LEFT JOIN dbo.tbl_Room PR ON PR.Id = RR.PreferredRoomId " +
                "WHERE RR.Id = @Id;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@Id", id)));
                    if (table.Rows.Count == 0)
                        return null;

                    return MapRequest(table.Rows[0]);
                }
            }
            catch (SqlException ex)
            {
                throw Failure("load request " + id, ex);
            }
        }

        private static List<RequestRoom> MapRequests(DataTable table)
        {
            var requests = new List<RequestRoom>();
            foreach (DataRow row in table.Rows)
                requests.Add(MapRequest(row));

            return requests;
        }

        private static RequestRoom MapRequest(DataRow row)
        {
            var request = new RequestRoom
            {
                Id = Convert.ToInt32(row["Id"]),
                Status = Convert.ToString(row["Status"]),
                RequestDate = Convert.ToDateTime(row["RequestDate"]),
                RoomName = Convert.ToString(row["RoomName"]),
                PreferredRoomName = Convert.ToString(row["PreferredRoomName"])
            };

            // Not every query selects these, so read them only when present.
            DataColumnCollection columns = row.Table.Columns;
            if (columns.Contains("PreferredRoomId") && row["PreferredRoomId"] != DBNull.Value)
                request.PreferredRoomId = Convert.ToInt32(row["PreferredRoomId"]);
            if (columns.Contains("UserName"))
                request.UserName = Convert.ToString(row["UserName"]);

            return request;
        }
    }
}
