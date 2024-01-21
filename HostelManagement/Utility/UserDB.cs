using System;
using System.Collections;
using System.Collections.Generic;
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
                //string Insertstr = "INSERT INTO MCQ_CollegeGroupDetails(CollegeGroupId,StudentId) " +
                //                          "values(@CollegeGroupId,@StudentId);Select cast(@@Identity as int);";

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
    }
}