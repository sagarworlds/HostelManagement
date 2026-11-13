/*
    HostelHub - back up the database to a file, and check the file.

    A backup is the only thing that brings the data back after a failed disk, a mistaken delete or a bad upgrade. Take
    one every day and before every upgrade, keep copies on another disk or machine, and practise restoring one
    (Database/restore.sql) so that the first time you do it is not during an emergency.

    How:
      1. Create the folder for the backups. The account SQL Server runs as must be allowed to write to it.
      2. Set @BackupFolder below (and @DatabaseName if yours is not called HMSDB) and run the script as an
         administrator, for example in SSMS. To run it every day, use Task Scheduler with sqlcmd (docs/deploying.md).

    Each run writes a new file called <database>_<date>_<time>.bak (the time runs to the millisecond, so no two runs share
    a name). Nothing is overwritten and nothing is deleted: remove old files yourself (docs/deploying.md shows how). The file is written with page checksums and then checked against
    them (RESTORE VERIFYONLY), so a damaged backup is found now rather than when it is needed. The last result shows the
    full path of the file.
*/

USE master;
GO

DECLARE @DatabaseName NVARCHAR(128) = N'HMSDB';
DECLARE @BackupFolder NVARCHAR(260) = N'C:\HostelHubBackups';
DECLARE @Compress     BIT           = 0;   -- 1 = smaller files; SQL Server Express cannot do this

-- A failed BACKUP only ends its own statement; by default the script would carry on and still finish with "Backup written
-- and verified" under the error messages. With this it stops at the first error, which is shown in full with its cause.
SET XACT_ABORT ON;

IF DB_ID(@DatabaseName) IS NULL
    THROW 50010, 'There is no database with that name on this server. Check @DatabaseName.', 1;

-- EngineEdition 4 is Express. It can restore compressed backups but not make them.
IF @Compress = 1 AND CAST(SERVERPROPERTY('EngineEdition') AS INT) = 4
    THROW 50011, 'SQL Server Express cannot create compressed backups. Set @Compress to 0.', 1;

-- Paths on Linux use a slash, on Windows a backslash.
DECLARE @separator NCHAR(1) = CASE WHEN CHARINDEX(N'Linux', @@VERSION) > 0 THEN N'/' ELSE N'\' END;
IF RIGHT(@BackupFolder, 1) IN (N'\', N'/')
    SET @BackupFolder = LEFT(@BackupFolder, LEN(@BackupFolder) - 1);

-- Down to the millisecond, so that no two runs can meet at one file name: a second run that did would silently overwrite
-- the first (BACKUP ... INIT), or fail if one of the two was compressed and the other was not.
DECLARE @now DATETIME2(3) = SYSDATETIME();
DECLARE @stamp NVARCHAR(30) = CONVERT(NVARCHAR(8), @now, 112) + N'_' + REPLACE(CONVERT(NVARCHAR(8), @now, 108), N':', N'')
                            + N'_' + RIGHT(CONVERT(NVARCHAR(12), @now, 114), 3);
DECLARE @file NVARCHAR(400) = @BackupFolder + @separator + @DatabaseName + N'_' + @stamp + N'.bak';
DECLARE @description NVARCHAR(128) = N'HostelHub backup of ' + @DatabaseName;

IF @Compress = 1
    BACKUP DATABASE @DatabaseName TO DISK = @file WITH CHECKSUM, COMPRESSION, INIT, NAME = @description, STATS = 50;
ELSE
    BACKUP DATABASE @DatabaseName TO DISK = @file WITH CHECKSUM, NO_COMPRESSION, INIT, NAME = @description, STATS = 50;

-- Reads the whole file back and checks every page against its checksum. Fails the script if anything is wrong.
RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;

-- In the FULL recovery model the transaction log grows until the log itself is backed up. Databases made by schema.sql
-- use SIMPLE, but one made some other way may not.
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = @DatabaseName) = N'FULL'
   AND NOT EXISTS (SELECT 1 FROM msdb.dbo.backupset WHERE database_name = @DatabaseName AND type = 'L')
    PRINT N'Note: this database uses the FULL recovery model but its transaction log has never been backed up, so the log '
        + N'keeps growing. Either schedule log backups, or, if losing up to a day of changes is acceptable, run: '
        + N'ALTER DATABASE ' + QUOTENAME(@DatabaseName) + N' SET RECOVERY SIMPLE;';

PRINT N'Backup written and verified: ' + @file;
SELECT @file AS BackupFile;
GO
