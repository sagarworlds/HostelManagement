using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace HostelManagement
{
    public class UserDB
    {

        public bool AddUser(User oUser)
        {
            DBAccess oDBAccess = null;
            int Id = 0;
            try
            {
                oDBAccess = new DBAccess();
                oUser.Password = PasswordUtility.HashPassword(oUser.Password);
                string Insertstr = "INSERT INTO [dbo].[tbl_User]  ([Email],[Password],[UserType],[FirstName],[LastName],[MobileNo],[CreatedOn],[ModifiedOn])" +
                    "  VALUES(@Email, @Password, @UserType,@FirstName,@LastName,@MobileNo,@CreatedOn,@ModifiedOn);Select cast(@@Identity as int);";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@Email", Value = oUser.Email });
                oParameters.Add(new SqlParameter { ParameterName = "@Password", Value = oUser.Password });
                oParameters.Add(new SqlParameter { ParameterName = "@UserType", Value = oUser.UserType });
                oParameters.Add(new SqlParameter { ParameterName = "@FirstName", Value = oUser.FirstName });
                oParameters.Add(new SqlParameter { ParameterName = "@LastName", Value = oUser.LastName });
                oParameters.Add(new SqlParameter { ParameterName = "@MobileNo", Value = oUser.MobileNo });
                oParameters.Add(new SqlParameter { ParameterName = "@CreatedOn", Value = oUser.CreatedOn });
                oParameters.Add(new SqlParameter { ParameterName = "@ModifiedOn", Value = oUser.ModifiedOn });

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
        public User Authenticate(User oUser)
        {
            DBAccess oDBAccess = null;
            User user = null;
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT Id, [Email],[Password],[UserType],[FirstName],[LastName],[MobileNo],[CreatedOn],[ModifiedOn] FROM [dbo].[tbl_User] " +
                    "WHERE Email=@Email";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@Email", Value = oUser.Email });
                var oDataTable = oDBAccess.lfnGetDataTable(sql, oParameters);
                if (oDataTable.Rows.Count > 0)
                {
                    var row = oDataTable.Rows[0];
                    string hashedPassword = Convert.ToString(row["Password"]);
                    if (PasswordUtility.VerifyPassword(oUser.Password, hashedPassword))
                    {
                        user = new User();
                        user.Id = Convert.ToInt32(row["Id"]);
                        user.Email = Convert.ToString(row["Email"]);
                        user.UserType = Convert.ToString(row["UserType"]);
                        user.UserType = Convert.ToString(row["UserType"]);
                        user.FirstName = Convert.ToString(row["FirstName"]);
                        user.LastName = Convert.ToString(row["LastName"]);
                        user.MobileNo = Convert.ToString(row["MobileNo"]);
                    }
                }

            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return user;
        }
        public bool EmailExists(User oUser)
        {
            DBAccess oDBAccess = null;
            bool oResult = false;
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT  [Email],[Password],[UserType],[FirstName],[LastName],[MobileNo],[CreatedOn],[ModifiedOn] FROM [dbo].[tbl_User] " +
                    "WHERE Email=@Email";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@Email", Value = oUser.Email });
                var oDataTable = oDBAccess.lfnGetDataTable(sql, oParameters);

                oResult = oDataTable.Rows.Count > 0;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return oResult;
        }
        public List<User> GetAdminList(string userType="Admin")
        {
            DBAccess oDBAccess = null;
            List<User> oUserList = new List<User>();
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT Id,FirstName+' '+LastName AS UserName,Email FROM tbl_User Where UserType=@userType";
                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@userType", Value = userType });
                var oDataTable = oDBAccess.lfnGetDataTable(sql,oParameters);

                for (int i = 0; i < oDataTable.Rows.Count; i++)
                {
                    var row = oDataTable.Rows[i];
                    var oUser = new User();
                    oUser.Id = Convert.ToInt32(row["Id"]);
                    oUser.UserName = Convert.ToString(row["UserName"]);
                    oUser.Email = Convert.ToString(row["Email"]);
                    oUserList.Add(oUser);
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return oUserList;
        }
        public bool DeleteUser(int Id)
        {
            bool bresult = false;
            DBAccess oDBAccess = null;
            try
            {
                oDBAccess = new DBAccess();
                string DeleteStr = "Delete From tbl_User Where Id =@Id";

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
        public bool DeleteAdminUser(int Id)
        {
            bool bresult = false;
            DBAccess oDBAccess = null;
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT  * FROM [dbo].[tbl_User] where UserType='Admin'";
                var oDataTable = oDBAccess.lfnGetDataTable(sql);

                var oResult = oDataTable.Rows.Count > 1;
                if (oResult)
                {
                    string DeleteStr = "Delete From tbl_User Where Id =@Id";
                    ArrayList oParameters = new ArrayList();
                    oParameters.Add(new SqlParameter { ParameterName = "@Id", SqlDbType = SqlDbType.Int, Value = Id });
                    oDBAccess.lfnUpdateData(DeleteStr, oParameters);
                    bresult = true;
                }
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

        public User GetUserById(int Id)
        {
            DBAccess oDBAccess = null;
            User user = null;
            try
            {
                oDBAccess = new DBAccess();
                string sql = "SELECT Id, [Email],[Password],[UserType],[FirstName],[LastName],[MobileNo],[CreatedOn],[ModifiedOn] FROM [dbo].[tbl_User] " +
                    "WHERE Id=@Id";

                ArrayList oParameters = new ArrayList();
                oParameters.Add(new SqlParameter { ParameterName = "@Id", Value = Id });
                var oDataTable = oDBAccess.lfnGetDataTable(sql, oParameters);
                if (oDataTable.Rows.Count > 0)
                {
                    var row = oDataTable.Rows[0];
                    user = new User();
                    user.Id = Convert.ToInt32(row["Id"]);
                    user.Email = Convert.ToString(row["Email"]);
                    user.UserType = Convert.ToString(row["UserType"]);
                    user.UserType = Convert.ToString(row["UserType"]);
                    user.FirstName = Convert.ToString(row["FirstName"]);
                    user.LastName = Convert.ToString(row["LastName"]);
                    user.MobileNo = Convert.ToString(row["MobileNo"]);
                }

            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (oDBAccess != null && oDBAccess.isConnectionOpen()) oDBAccess.CloseDB();
            }
            return user;
        }


    }
}