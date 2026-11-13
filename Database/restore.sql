/*
    HostelHub - restore the database from a backup file made by backup.sql.

    Two uses:

      PRACTICE (do this now and then, so the first restore is not an emergency):
        restore under another name, beside the live database. Nothing live is touched. Look at the copy, then drop it
        with   DROP DATABASE HMSDB_Practice;
          @RestoreAs = N'HMSDB_Practice'    @ReplaceExisting = 0

      RECOVERY (after a disaster, or to undo a bad upgrade):
        put the backed-up data back as the live database. Everyone connected to it is disconnected first, and
        everything changed since the backup was taken is lost.
          @RestoreAs = N'HMSDB'             @ReplaceExisting = 1

    How: set the values below and run the script as an administrator, for example in SSMS. Needs SQL Server 2012 or
    later. The backup file is checked before anything is changed, and the restored database is checked afterwards
    (DBCC CHECKDB). The file must be one that carries page checksums, as every backup made by backup.sql does: that is
    what lets damage be found. A backup made some other way without them is refused (error 3187) before anything changes.

    Replacing a database is a one-way step: once SQL Server starts, the old contents are gone and the database cannot be
    used until the restore completes. So, before replacing, the script saves a safety copy of the current database
    (<name>_before_restore_<time>.bak, next to the backup file) and stops, showing the reason, if it cannot: the folder
    may not be writable for SQL Server, the disk may be full, or the database may be offline or damaged. If you accept
    losing a database that is too damaged to copy, set @SafetyBackup to 0. If a restore fails part-way, fix the cause
    named in the error messages and run the script again with the same settings: it carries on and completes the restore.

    After restoring onto a DIFFERENT server, the application's login does not exist there yet: run
    Database/create-app-login.sql in the restored database to create it and connect it again.
*/

USE master;
GO

DECLARE @BackupFile      NVARCHAR(400) = N'C:\HostelHubBackups\HMSDB_20260101_020000.bak';
DECLARE @RestoreAs       NVARCHAR(128) = N'HMSDB_Practice';
DECLARE @ReplaceExisting BIT           = 0;
DECLARE @SafetyBackup    BIT           = 1;   -- save the current database first when replacing it; 0 only if it is too damaged to copy

-- A failed BACKUP or RESTORE only ends its own statement; by default the script would carry on to the next step, which
-- here is the destructive one. With this it stops at the first error, which is shown in full with its cause.
SET XACT_ABORT ON;

IF @RestoreAs IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 50020, 'Choose another name: a system database cannot be restored from a HostelHub backup.', 1;

IF DB_ID(@RestoreAs) IS NOT NULL AND @ReplaceExisting = 0
    THROW 50021, 'A database with that name already exists. Choose another name, or set @ReplaceExisting to 1 to replace it.', 1;

-- Reads the whole file and checks its checksums before anything is touched. Fails here if the file is missing or damaged.
RESTORE VERIFYONLY FROM DISK = @BackupFile WITH CHECKSUM;

DECLARE @quotedFile NVARCHAR(500) = N'N''' + REPLACE(@BackupFile, N'''', N'''''') + N'''';
DECLARE @quotedName NVARCHAR(300) = QUOTENAME(@RestoreAs);
DECLARE @sql NVARCHAR(MAX);

-- The files inside the backup. RESTORE FILELISTONLY returns one more column from SQL Server 2016 on (SnapshotUrl).
CREATE TABLE #files
(
    LogicalName NVARCHAR(128), PhysicalName NVARCHAR(260), [Type] CHAR(1), FileGroupName NVARCHAR(128) NULL,
    Size NUMERIC(20, 0), MaxSize NUMERIC(20, 0), FileId BIGINT, CreateLSN NUMERIC(25, 0), DropLSN NUMERIC(25, 0) NULL,
    UniqueId UNIQUEIDENTIFIER, ReadOnlyLSN NUMERIC(25, 0) NULL, ReadWriteLSN NUMERIC(25, 0) NULL,
    BackupSizeInBytes BIGINT, SourceBlockSize INT, FileGroupId INT, LogGroupGUID UNIQUEIDENTIFIER NULL,
    DifferentialBaseLSN NUMERIC(25, 0) NULL, DifferentialBaseGUID UNIQUEIDENTIFIER NULL, IsReadOnly BIT, IsPresent BIT,
    TDEThumbprint VARBINARY(32) NULL
);
IF CAST(PARSENAME(CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(32)), 4) AS INT) >= 13
    ALTER TABLE #files ADD SnapshotUrl NVARCHAR(360) NULL;
SET @sql = N'RESTORE FILELISTONLY FROM DISK = ' + @quotedFile + N';';
INSERT INTO #files EXEC (@sql);

-- Where new files go: the server's default folders, or, failing that, next to the master database.
DECLARE @masterFile NVARCHAR(260) = (SELECT physical_name FROM sys.master_files WHERE database_id = 1 AND file_id = 1);
DECLARE @separator NCHAR(1) = CASE WHEN CHARINDEX(N'Linux', @@VERSION) > 0 THEN N'/' ELSE N'\' END;
DECLARE @dataFolder NVARCHAR(260) = COALESCE(CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(260)),
    LEFT(@masterFile, LEN(@masterFile) - CHARINDEX(@separator, REVERSE(@masterFile)) + 1));
DECLARE @logFolder NVARCHAR(260) = COALESCE(CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS NVARCHAR(260)), @dataFolder);

-- One MOVE per file. When replacing a database, each file goes back where the existing database keeps its file of that
-- name; otherwise (a practice copy, or a new server) each gets a name built from @RestoreAs in the default folder.
DECLARE @moves NVARCHAR(MAX) = N'';
DECLARE @logicalName NVARCHAR(128), @fileType CHAR(1), @existingPath NVARCHAR(260), @target NVARCHAR(260), @dataFiles INT = 0;
DECLARE backupFiles CURSOR LOCAL FAST_FORWARD FOR SELECT LogicalName, [Type] FROM #files ORDER BY FileId;
OPEN backupFiles;
FETCH NEXT FROM backupFiles INTO @logicalName, @fileType;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @existingPath = (SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(@RestoreAs) AND name = @logicalName);

    IF @existingPath IS NOT NULL
        SET @target = @existingPath;
    ELSE IF @fileType = 'L'
        SET @target = @logFolder + @RestoreAs + N'_log.ldf';
    ELSE
    BEGIN
        SET @dataFiles = @dataFiles + 1;
        SET @target = @dataFolder + @RestoreAs + CASE WHEN @dataFiles = 1 THEN N'.mdf' ELSE N'_' + CAST(@dataFiles AS NVARCHAR(10)) + N'.ndf' END;
    END

    SET @moves = @moves + N'MOVE N''' + REPLACE(@logicalName, N'''', N'''''') + N''' TO N''' + REPLACE(@target, N'''', N'''''') + N''', ';
    FETCH NEXT FROM backupFiles INTO @logicalName, @fileType;
END
CLOSE backupFiles;
DEALLOCATE backupFiles;
DROP TABLE #files;

SET @sql = N'RESTORE DATABASE ' + @quotedName + N' FROM DISK = ' + @quotedFile + N' WITH ' + @moves + N'CHECKSUM, RECOVERY'
         + CASE WHEN @ReplaceExisting = 1 THEN N', REPLACE;' ELSE N';' END;

-- NULL when there is no database of that name. A database left RESTORING by an earlier failed attempt can only be
-- restored over; it cannot be altered or backed up.
DECLARE @state NVARCHAR(60) = (SELECT state_desc FROM sys.databases WHERE name = @RestoreAs);
DECLARE @replacing BIT = CASE WHEN @state IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @online BIT = CASE WHEN @state = N'ONLINE' THEN 1 ELSE 0 END;

IF @replacing = 1 AND @state <> N'RESTORING' AND @SafetyBackup = 1
BEGIN
    DECLARE @folder NVARCHAR(400) = LEFT(@BackupFile, LEN(@BackupFile) - CHARINDEX(@separator, REVERSE(@BackupFile)) + 1);
    -- Down to the millisecond, as in backup.sql: a safety copy must never overwrite an earlier one.
    DECLARE @now DATETIME2(3) = SYSDATETIME();
    DECLARE @stamp NVARCHAR(30) = CONVERT(NVARCHAR(8), @now, 112) + N'_' + REPLACE(CONVERT(NVARCHAR(8), @now, 108), N':', N'')
                                + N'_' + RIGHT(CONVERT(NVARCHAR(12), @now, 114), 3);
    DECLARE @safetyFile NVARCHAR(500) = @folder + @RestoreAs + N'_before_restore_' + @stamp + N'.bak';

    PRINT N'Saving a safety copy of ' + @RestoreAs + N' first. If that fails, the database has not been touched: fix the cause '
        + N'shown, or set @SafetyBackup to 0 to restore without a copy.';

    -- Deliberately not inside TRY/CATCH: a catch block only sees "BACKUP DATABASE is terminating abnormally" and loses the
    -- reason (folder not writable, disk full, database offline). Unprotected, XACT_ABORT stops the script right here and
    -- the client shows every message, and nothing has been replaced yet.
    BACKUP DATABASE @RestoreAs TO DISK = @safetyFile WITH CHECKSUM, COPY_ONLY, INIT, NAME = N'HostelHub safety copy before a restore';

    PRINT N'Safety copy of the current database: ' + @safetyFile;
END;

BEGIN TRY
    -- Replacing the live database: send everyone away first (their open transactions are rolled back).
    IF @online = 1
        EXEC (N'ALTER DATABASE ' + @quotedName + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE;');

    EXEC sys.sp_executesql @sql;

    -- Not only after replacing: a backup taken while the database was in single-user mode brings that mode back with it,
    -- and a copy that only one connection can use at a time (SSMS holds one itself) looks broken.
    EXEC (N'ALTER DATABASE ' + @quotedName + N' SET MULTI_USER;');
END TRY
BEGIN CATCH
    IF @replacing = 1 AND (SELECT state_desc FROM sys.databases WHERE name = @RestoreAs) = N'ONLINE'
    BEGIN
        -- The restore did not get as far as touching the database: open it to everyone again.
        BEGIN TRY
            EXEC (N'ALTER DATABASE ' + @quotedName + N' SET MULTI_USER;');
        END TRY
        BEGIN CATCH
            SET @sql = NULL;   -- nothing more can be done here; the original error is reported below
        END CATCH;
    END
    ELSE IF DB_ID(@RestoreAs) IS NOT NULL
    BEGIN
        PRINT N'The restore failed after SQL Server had started replacing ' + @RestoreAs + N', which is now in the RESTORING state '
            + N'and cannot be used yet. Fix the cause named in the error messages and run this script again with the same settings: '
            + N'it will complete the restore. (If this was a practice restore, drop the unfinished copy instead: DROP DATABASE '
            + @quotedName + N';)';
    END;

    THROW;
END CATCH;

-- The restored copy is read through completely. Silent when healthy; reports errors if the data is damaged.
DBCC CHECKDB (@RestoreAs) WITH NO_INFOMSGS;

PRINT N'Restored ' + @RestoreAs + N' from ' + @BackupFile + N'.';
SELECT name AS RestoredAs, state_desc AS State, user_access_desc AS Access, recovery_model_desc AS RecoveryModel
FROM sys.databases WHERE name = @RestoreAs;
GO
