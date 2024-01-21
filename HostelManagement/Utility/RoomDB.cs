using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Policy;
using System.Web;

namespace HostelManagement
{
    public class RoomDB
    {
        public bool AddRoom(Room oRoom)
        {
            DBAccess oDBAccess = null;
            int Id = 0;
            try
            {
                oDBAccess = new DBAccess();
                string Insertstr = "INSERT INTO tbl_Room ([RoomName],[Rent],[Description],[ModifiedBy],[CreatedOn],[ModifiedOn])" +
                    "  VALUES(@RoomName, @Rent, @Description,@ModifiedBy,@CreatedOn,@ModifiedOn);Select cast(@@Identity as int);";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@RoomName", Value = oRoom.RoomName });
                oParameters.Add(new SqlParameter { ParameterName = "@Rent", Value = oRoom.Rent });
                oParameters.Add(new SqlParameter { ParameterName = "@Description", Value = oRoom.Description });
                oParameters.Add(new SqlParameter { ParameterName = "@ModifiedBy", Value = oRoom.ModifiedBy });
                oParameters.Add(new SqlParameter { ParameterName = "@CreatedOn", Value = oRoom.CreatedOn });
                oParameters.Add(new SqlParameter { ParameterName = "@ModifiedOn", Value = oRoom.ModifiedOn });

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

        public bool UpdateRoom(Room oRoom)
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

        public bool DeleteRoom(int Id)
        {
            bool bresult = false;
            DBAccess oDBAccess = null;
            try
            {
                oDBAccess = new DBAccess();
                string DeleteStr = "Delete From tbl_Room Where Id =@Id";

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

        public List<Room> GetRoomList()
        {
            DBAccess oDBAccess = null;
            bool oResult = false;
            List<Room> oRoomList = new List<Room>();
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT * FROM tbl_Room";

                var oDataTable = oDBAccess.lfnGetDataTable(sql);

                for (int i = 0; i < oDataTable.Rows.Count; i++)
                {
                    var row = oDataTable.Rows[i];
                    var oRoom = new Room();
                    oRoom.Id = Convert.ToInt32(row["Id"]);
                    oRoom.RoomName = Convert.ToString(row["RoomName"]);
                    oRoom.Rent = Convert.ToInt32(row["Rent"]);
                    oRoom.Description = Convert.ToString(row["Description"]);
                    oRoomList.Add(oRoom);
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
            return oRoomList;
        }

        public Room GetRoomById(int Id)
        {
            DBAccess oDBAccess = null;
            Room oRoom = null;

            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT * FROM tbl_Room WHERE Id=@Id";
                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@Id", Value = Id });
                var oDataTable = oDBAccess.lfnGetDataTable(sql, oParameters);

                if (oDataTable.Rows.Count > 0)
                {
                    oRoom = new Room();
                    var row = oDataTable.Rows[0];
                    oRoom.Id = Convert.ToInt32(row["Id"]);
                    oRoom.RoomName = Convert.ToString(row["RoomName"]);
                    oRoom.Rent = Convert.ToInt32(row["Rent"]);
                    oRoom.Description = Convert.ToString(row["Description"]);
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return oRoom;
        }


    }
}