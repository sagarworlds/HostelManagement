using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace HostelManagement
{
    /// <summary>SQL Server implementation of <see cref="IAnnouncementRepository"/>.</summary>
    public class AnnouncementDB : RepositoryBase, IAnnouncementRepository
    {
        public AnnouncementDB(ILogger logger) : base(logger)
        {
        }

        public OperationResult Add(Announcement announcement)
        {
            const string sql =
                "INSERT INTO dbo.tbl_Announcement (Title, Body, CreatedBy, CreatedOn, ExpiresOn) " +
                "VALUES (@Title, @Body, @CreatedBy, @CreatedOn, @ExpiresOn);";

            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery(sql, Params(
                        Param("@Title", announcement.Title),
                        Param("@Body", announcement.Body),
                        Param("@CreatedBy", announcement.CreatedBy),
                        Param("@CreatedOn", announcement.CreatedOn),
                        Param("@ExpiresOn", SqlDbType.DateTime, announcement.ExpiresOn)));

                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail(GenericWriteFailureMessage);
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("add announcement", ex);
            }
        }

        public OperationResult Delete(int id)
        {
            try
            {
                using (var db = new DBAccess())
                {
                    int rows = db.lfnExecuteNonQuery("DELETE FROM dbo.tbl_Announcement WHERE Id = @Id;", Params(Param("@Id", id)));
                    return rows == 1 ? OperationResult.Ok() : OperationResult.Fail("This announcement no longer exists.");
                }
            }
            catch (SqlException ex)
            {
                return FailureResult("delete announcement " + id, ex);
            }
        }

        public List<Announcement> GetActive(int maximum, DateTime now)
        {
            const string sql =
                "SELECT TOP (@Maximum) Id, Title, Body, CreatedBy, CreatedOn, ExpiresOn FROM dbo.tbl_Announcement " +
                "WHERE ExpiresOn IS NULL OR ExpiresOn > @Now ORDER BY CreatedOn DESC, Id DESC;";

            try
            {
                using (var db = new DBAccess())
                {
                    return Map(db.lfnGetDataTable(sql, Params(Param("@Maximum", maximum), Param("@Now", now))));
                }
            }
            catch (SqlException ex)
            {
                throw Failure("list current announcements", ex);
            }
        }

        public List<Announcement> GetAll()
        {
            const string sql =
                "SELECT Id, Title, Body, CreatedBy, CreatedOn, ExpiresOn FROM dbo.tbl_Announcement ORDER BY CreatedOn DESC, Id DESC;";

            try
            {
                using (var db = new DBAccess())
                {
                    return Map(db.lfnGetDataTable(sql));
                }
            }
            catch (SqlException ex)
            {
                throw Failure("list announcements", ex);
            }
        }

        private static List<Announcement> Map(DataTable table)
        {
            var announcements = new List<Announcement>();
            foreach (DataRow row in table.Rows)
            {
                announcements.Add(new Announcement
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Title = Convert.ToString(row["Title"]),
                    Body = Convert.ToString(row["Body"]),
                    CreatedBy = row["CreatedBy"] == DBNull.Value ? 0 : Convert.ToInt32(row["CreatedBy"]),
                    CreatedOn = Convert.ToDateTime(row["CreatedOn"]),
                    ExpiresOn = row["ExpiresOn"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ExpiresOn"])
                });
            }

            return announcements;
        }
    }
}
