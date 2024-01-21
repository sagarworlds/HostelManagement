using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Web;

namespace HostelManagement.Utility
{
    public class RequestRoomDB
    {
        public bool AddRoomRequest(RequestRoom oRequestRoom)
        {
            int Id = 0;
            DBAccess oDBAccess = null;
            try
            {
                oDBAccess = new DBAccess();
                string Insertstr = "INSERT INTO tbl_RequestRoom ([UserId],[Status],[CreatedOn],[RequestDate])" +
                    "  VALUES(@UserId, @Status,@CreatedOn,@RequestDate);Select cast(@@Identity as int);";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@UserId", Value = oRequestRoom.UserId });
                oParameters.Add(new SqlParameter { ParameterName = "@Status", Value = oRequestRoom.Status });
                oParameters.Add(new SqlParameter { ParameterName = "@CreatedOn", Value = oRequestRoom.CreatedOn });
                oParameters.Add(new SqlParameter { ParameterName = "@RequestDate", Value = oRequestRoom.RequestDate });

                Id = oDBAccess.lfnExecuteScaler<int>(Insertstr, oParameters);
            }
            catch (Exception ex)
            {
                Id = 0;
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return Id > 0;
        }

        public bool UpdateRoomRequest(Room oRoom)
        {
            bool bresult = false; DBAccess oDBAccess = null;
            try
            {
                oDBAccess = new DBAccess();
                string UpdateStr = "UPDATE tbl_Room SET RoomName=@RoomName,Rent=@Rent,Description=@Description,ModifiedBy=@ModifiedBy,ModifiedOn=@ModifiedOn Where Id=@Id";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter() { ParameterName = "@RoomName", Value = oRoom.RoomName });
                oParameters.Add(new SqlParameter() { ParameterName = "@Rent", Value = oRoom.Rent });
                oParameters.Add(new SqlParameter() { ParameterName = "@Description", Value = oRoom.Description });
                oParameters.Add(new SqlParameter() { ParameterName = "@ModifiedBy", Value = oRoom.ModifiedBy });
                oParameters.Add(new SqlParameter() { ParameterName = "@ModifiedOn", Value = oRoom.ModifiedOn });
                oParameters.Add(new SqlParameter() { ParameterName = "@Id", Value = oRoom.Id });

                oDBAccess.lfnUpdateData(UpdateStr, oParameters);
                bresult = true;
            }
            catch (Exception ex)
            {
                //
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return bresult;

        }

        public bool DeleteRequestRoom(int Id)
        {
            bool bresult = false;
            DBAccess oDBAccess = null;
            try
            {
                oDBAccess = new DBAccess();
                string DeleteStr = "Delete From tbl_RequestRoom Where Id =@Id";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@Id", SqlDbType = SqlDbType.Int, Value = Id });
                oDBAccess.lfnUpdateData(DeleteStr, oParameters);
                bresult = true;

            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return bresult;
        }

        public List<RequestRoom> GetRoomRequestListByUserId(int UserId)
        {
            DBAccess oDBAccess = null;
            bool oResult = false;
            List<RequestRoom> oRequestRoomList = new List<RequestRoom>();
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT * FROM tbl_RequestRoom LEFT JOIN tbl_Room ON tbl_Room.Id=tbl_RequestRoom.ApprovedRoomId WHERE UserId=@UserId";
                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@UserId", SqlDbType = SqlDbType.Int, Value = UserId });
                var oDataTable = oDBAccess.lfnGetDataTable(sql, oParameters);

                for (int i = 0; i < oDataTable.Rows.Count; i++)
                {
                    var row = oDataTable.Rows[i];
                    var oRequestRoom = new RequestRoom();
                    oRequestRoom.Id = Convert.ToInt32(row["Id"]);
                    oRequestRoom.Status = Convert.ToString(row["Status"]);
                    oRequestRoom.RoomName = Convert.ToString(row["RoomName"]);
                    oRequestRoomList.Add(oRequestRoom);
                }
                oResult = oDataTable.Rows.Count > 0;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return oRequestRoomList;
        }


        public List<RequestRoom> GetRoomRequestList()
        {
            DBAccess oDBAccess = null;
            bool oResult = false;
            List<RequestRoom> oRequestRoomList = new List<RequestRoom>();
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT * FROM tbl_RequestRoom LEFT JOIN tbl_Room ON tbl_Room.Id=tbl_RequestRoom.ApprovedRoomId";

                var oDataTable = oDBAccess.lfnGetDataTable(sql);

                for (int i = 0; i < oDataTable.Rows.Count; i++)
                {
                    var row = oDataTable.Rows[i];
                    var oRequestRoom = new RequestRoom();
                    oRequestRoom.Id = Convert.ToInt32(row["Id"]);
                    oRequestRoom.Status = Convert.ToString(row["Status"]);
                    oRequestRoom.RoomName = Convert.ToString(row["RoomName"]);
                    oRequestRoomList.Add(oRequestRoom);
                }
                oResult = oDataTable.Rows.Count > 0;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return oRequestRoomList;
        }

        public RequestRoom GetRoomRequestListById(int Id)
        {
            DBAccess oDBAccess = null;
            bool oResult = false;
            RequestRoom oRequestRoom = null;
            try
            {
                oDBAccess = new DBAccess();
                string sql = "select TRR.Id, TRR.Status,TU.FirstName + ' ' + Tu.LastName as UserName from tbl_RequestRoom TRR INNER JOIN tbl_User TU ON TU.Id = TRR.UserId where TRR.Id = @Id";
                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@Id", SqlDbType = SqlDbType.Int, Value = Id });
                var oDataTable = oDBAccess.lfnGetDataTable(sql, oParameters);

                if (oDataTable.Rows.Count > 0)
                {
                    var row = oDataTable.Rows[0];
                    oRequestRoom = new RequestRoom();
                    oRequestRoom.Id = Convert.ToInt32(row["Id"]);
                    oRequestRoom.Status = Convert.ToString(row["Status"]);
                    //oRequestRoom.RoomName = Convert.ToString(row["RoomName"]);
                    oRequestRoom.UserName = Convert.ToString(row["UserName"]);
                }
                oResult = oDataTable.Rows.Count > 0;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return oRequestRoom;
        }


    }
}