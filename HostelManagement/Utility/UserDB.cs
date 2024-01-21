using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

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
                string sql = "SELECT  [Email],[Password],[UserType],[FirstName],[LastName],[MobileNo],[CreatedOn],[ModifiedOn] FROM [dbo].[tbl_User] " +
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

    }
}