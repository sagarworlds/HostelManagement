using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HostelManagement.Utility;

namespace HostelManagement
{
    /// <summary>SQL Server implementation of <see cref="IRoomRepository"/>.</summary>
    public class RoomDB : RepositoryBase, IRoomRepository
    {
        public RoomDB(ILogger logger) : base(logger)
        {
        }

        public OperationResult AddRoom(Room room)
        {
            // Availability is set explicitly: a new room is free. Relying on a column default left rooms
            // created without a value (NULL), which are never offered to students.
            const string sql =
                "INSERT INTO dbo.tbl_Room (RoomName, Rent, [Description], Availability, ModifiedBy, CreatedOn, ModifiedOn) " +
                "VALUES (@RoomName, @Rent, @Description, 1, @ModifiedBy, @CreatedOn, @ModifiedOn);";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@RoomName", room.RoomName),
                        Param("@Rent", room.Rent),
                        Param("@Description", room.Description),
                        Param("@ModifiedBy", room.ModifiedBy),
                        Param("@CreatedOn", room.CreatedOn),
                        Param("@ModifiedOn", room.ModifiedOn)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail(GenericWriteFailureMessage);
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("add room", ex);
            }
        }

        public OperationResult UpdateRoom(Room room)
        {
            const string sql =
                "UPDATE dbo.tbl_Room SET RoomName = @RoomName, Rent = @Rent, [Description] = @Description, " +
                "ModifiedBy = @ModifiedBy, ModifiedOn = @ModifiedOn WHERE Id = @Id;";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@RoomName", room.RoomName),
                        Param("@Rent", room.Rent),
                        Param("@Description", room.Description),
                        Param("@ModifiedBy", room.ModifiedBy),
                        Param("@ModifiedOn", room.ModifiedOn),
                        Param("@Id", room.Id)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail("This room no longer exists.");
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("update room " + room.Id, ex);
            }
        }

        public OperationResult DeleteRoom(int id)
        {
            // The NOT EXISTS makes "a student holds this room" part of the delete itself, so a room cannot be
            // removed in the gap between checking and deleting.
            const string deleteSql =
                "DELETE FROM dbo.tbl_Room WHERE Id = @Id " +
                "AND NOT EXISTS (SELECT 1 FROM dbo.tbl_RequestRoom WHERE ApprovedRoomId = @Id AND Status = @Approved);";

            try
            {
                using (var db = new DBAccess())
                {
                    db.BeginTransaction();

                    // PreferredRoomId only records what a student asked for. Its foreign key does not cascade (SQL Server
                    // allows only one cascading key to the room table), so clear it here. If the delete below is refused,
                    // leaving the using block rolls this back.
                    db.lfnExecuteNonQuery(
                        "UPDATE dbo.tbl_RequestRoom SET PreferredRoomId = NULL WHERE PreferredRoomId = @Id;",
                        Params(Param("@Id", id)));

                    int rows = db.lfnExecuteNonQuery(deleteSql, Params(
                        Param("@Id", id),
                        Param("@Approved", RequestStatus.Approved)));
                    if (rows == 1)
                    {
                        db.CommitTransaction();
                        return OperationResult.Ok();
                    }

                    // Nothing was deleted: either the room is gone already or a student holds it.
                    int stillThere = db.lfnExecuteScaler<int>(
                        "SELECT COUNT(1) FROM dbo.tbl_Room WHERE Id = @Id;", Params(Param("@Id", id)));

                    return OperationResult.Fail(stillThere == 0
                        ? "This room no longer exists."
                        : "This room is currently allocated to a student. Vacate it before deleting it.");
                }
            }
            catch (SqlException ex)
            {
                if (IsForeignKeyViolation(ex))
                    return OperationResult.Fail("This room is referenced by other records and cannot be deleted.");

                return FailureResult("delete room " + id, ex);
            }
        }

        public List<Room> GetRoomList()
        {
            const string sql =
                "SELECT R.Id, R.RoomName, R.Rent, R.[Description], R.Availability, " +
                "       RR.Id AS ActiveRequestId, U.FirstName + ' ' + U.LastName AS OccupantName " +
                "FROM dbo.tbl_Room R " +
                "LEFT JOIN dbo.tbl_RequestRoom RR ON RR.ApprovedRoomId = R.Id AND RR.Status = @Approved " +
                "LEFT JOIN dbo.tbl_User U ON U.Id = RR.UserId " +
                "ORDER BY R.RoomName;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@Approved", RequestStatus.Approved)));

                    var rooms = new List<Room>();
                    foreach (DataRow row in table.Rows)
                    {
                        Room room = MapRoom(row);
                        if (row["ActiveRequestId"] != DBNull.Value)
                        {
                            room.ActiveRequestId = Convert.ToInt32(row["ActiveRequestId"]);
                            room.OccupantName = Convert.ToString(row["OccupantName"]);
                        }

                        rooms.Add(room);
                    }

                    return rooms;
                }
            }
            catch (SqlException ex)
            {
                throw Failure("list rooms", ex);
            }
        }

        public Room GetRoomById(int id)
        {
            const string sql =
                "SELECT Id, RoomName, Rent, [Description], Availability FROM dbo.tbl_Room WHERE Id = @Id;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql, Params(Param("@Id", id)));
                    return table.Rows.Count == 0 ? null : MapRoom(table.Rows[0]);
                }
            }
            catch (SqlException ex)
            {
                throw Failure("load room " + id, ex);
            }
        }

        public List<Room> GetAvailableRooms()
        {
            const string sql =
                "SELECT Id, RoomName, Rent, [Description], Availability FROM dbo.tbl_Room " +
                "WHERE Availability = 1 ORDER BY RoomName;";

            try
            {
                using (var db = new DBAccess())
                {
                    DataTable table = db.lfnGetDataTable(sql);

                    var rooms = new List<Room>();
                    foreach (DataRow row in table.Rows)
                        rooms.Add(MapRoom(row));

                    return rooms;
                }
            }
            catch (SqlException ex)
            {
                throw Failure("list available rooms", ex);
            }
        }

        private static Room MapRoom(DataRow row)
        {
            return new Room
            {
                Id = Convert.ToInt32(row["Id"]),
                RoomName = Convert.ToString(row["RoomName"]),
                Rent = Convert.ToInt32(row["Rent"]),
                Description = Convert.ToString(row["Description"]),
                // A NULL flag (rooms created by older versions) is treated as "not available".
                Availability = row["Availability"] != DBNull.Value && Convert.ToBoolean(row["Availability"])
            };
        }
    }
}
