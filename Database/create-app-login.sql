/*
    HostelHub - create the database login the web application signs in with (instead of sa).

    Why: the application only ever reads and writes rows in its own tables. It does not need sa or db_owner, and it
    never changes the schema. A flaw in the application, or a copy of connectionStrings.config that falls into the
    wrong hands, should not hand over more than that. The login created here can read and write the rows of this one
    database and nothing else: it cannot create, alter or drop tables, has no server-level rights, and cannot reach
    other databases' data.

    How:
      1. Choose a long random password (at least 12 characters; 20 or more is better) and put it in @Password below.
         Letters and digits are simplest; if it contains a single quote, type that quote twice, as in any T-SQL string.
      2. Run this script as an administrator (sa, or a member of sysadmin), for example in SSMS.
      3. Put the login and the same password in connectionStrings.config:
             <add name="DBConnection" providerName="System.Data.SqlClient"
                  connectionString="Data Source=YOUR_SERVER;Initial Catalog=HMSDB;User ID=hostelhub_app;Password=THE_PASSWORD;" />

    Safe to repeat, and each repeat leaves the same end state:
      * run it again with a new password to change the password (this is how you rotate it);
      * run it after restoring the database on another server to reconnect the database user to the login;
      * if the user was given extra roles by hand, they are taken away again.

    The login is a SQL Server login with a password. When IIS and SQL Server run on the same machine, the alternative is
    to give the application pool's Windows identity access instead, which needs no password at all (see
    docs/deploying.md).
*/

USE HMSDB;
GO

DECLARE @LoginName NVARCHAR(128) = N'hostelhub_app';
DECLARE @Password  NVARCHAR(128) = N'PASTE_PASSWORD_HERE';

-- Recognised by its shape rather than by repeating the placeholder text, so "Replace All" in an editor cannot
-- accidentally change this check as well.
IF @Password LIKE N'PASTE[_]%'
    THROW 50000, 'Replace @Password with a long random password before running this script.', 1;

IF LEN(@Password) < 12
    THROW 50001, 'Use a password of at least 12 characters.', 1;

-- The grants below are database-wide; never hand them out in a system database by mistake.
IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 50002, 'Run this script in the HostelHub database, not in a system database.', 1;

DECLARE @quotedName NVARCHAR(300) = QUOTENAME(@LoginName);
DECLARE @quotedDatabase NVARCHAR(300) = QUOTENAME(DB_NAME());
-- CREATE LOGIN takes the password as a literal, not a variable, so it is built into the statement with its quotes doubled.
DECLARE @passwordLiteral NVARCHAR(300) = N'N''' + REPLACE(@Password, N'''', N'''''') + N'''';
DECLARE @sql NVARCHAR(MAX);

-- CHECK_POLICY makes SQL Server refuse weak passwords. CHECK_EXPIRATION is off on purpose: a password that expires by
-- itself would one day take the site down with no warning; change it deliberately by running this script again.
SET @sql = CASE WHEN EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @LoginName AND type = 'S')
                THEN N'ALTER LOGIN ' ELSE N'CREATE LOGIN ' END
         + @quotedName + N' WITH PASSWORD = ' + @passwordLiteral
         + N', DEFAULT_DATABASE = ' + @quotedDatabase + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
EXEC sys.sp_executesql @sql;

-- The database user. A user with this name but a different SID is what a restored copy of the database leaves behind
-- (the login on the new server is a different login); ALTER USER ... WITH LOGIN reconnects it.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals d
               INNER JOIN sys.server_principals s ON s.sid = d.sid
               WHERE d.name = @LoginName AND s.name = @LoginName)
BEGIN
    SET @sql = CASE WHEN EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @LoginName)
                    THEN N'ALTER USER ' + @quotedName + N' WITH LOGIN = ' + @quotedName + N';'
                    ELSE N'CREATE USER ' + @quotedName + N' FOR LOGIN ' + @quotedName + N';' END;
    EXEC sys.sp_executesql @sql;
END

-- Exactly two roles: read every table and write every table. Membership of these roles covers tables added by later
-- upgrades too, so nothing has to be granted again after Database/upgrade-existing-db.sql.
SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + @quotedName + N'; ALTER ROLE db_datawriter ADD MEMBER ' + @quotedName + N';';
EXEC sys.sp_executesql @sql;

-- Anything else the user was given by hand (db_owner, db_ddladmin, ...) is taken away again.
SET @sql = N'';
SELECT @sql = @sql + N'ALTER ROLE ' + QUOTENAME(r.name) + N' DROP MEMBER ' + @quotedName + N'; '
FROM   sys.database_role_members m
       INNER JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
       INNER JOIN sys.database_principals u ON u.principal_id = m.member_principal_id
WHERE  u.name = @LoginName AND r.name NOT IN (N'db_datareader', N'db_datawriter');
IF LEN(@sql) > 0
    EXEC sys.sp_executesql @sql;

PRINT N'Login ' + @LoginName + N' can read and write the data in ' + DB_NAME() + N' and do nothing else.';
GO
