/*
    HostelHub - bring an EXISTING HMSDB database in line with the current application.

    Safe to run more than once. It only touches rows/objects that need correcting, and finishes with
    read-only diagnostics for problems that need a human decision.

    Run it BEFORE deploying a newer version of the application: steps 5 and 6 add columns that the
    current code reads, and sign-in and the request pages fail (and log "schema looks out of date")
    until they exist. Step 7 widens the password column for the new hash format; step 8 adds the table used by
    e-mailed password reset.

    Background: earlier versions of the application never set tbl_Room.Availability when a room was
    created, never recorded when a room became free again, and did not stop a room from being
    approved twice.
*/

USE HMSDB;
GO

/* 1. New rooms must default to "available". */
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.tbl_Room')
      AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.tbl_Room'), N'Availability', 'ColumnId'))
    ALTER TABLE dbo.tbl_Room ADD CONSTRAINT DF_tbl_Room_Availability DEFAULT (1) FOR Availability;
GO

/* 2. Rooms created without an Availability value were never offered to students. Free rooms are
      those with no active allocation. */
UPDATE R
SET    R.Availability = 1
FROM   dbo.tbl_Room R
WHERE  R.Availability IS NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.tbl_RequestRoom RR WHERE RR.ApprovedRoomId = R.Id AND RR.Status = N'Approved');
GO

/* 3. A room with an active allocation must not be offered again. */
UPDATE R
SET    R.Availability = 0
FROM   dbo.tbl_Room R
WHERE  (R.Availability IS NULL OR R.Availability <> 0)
  AND  EXISTS (SELECT 1 FROM dbo.tbl_RequestRoom RR WHERE RR.ApprovedRoomId = R.Id AND RR.Status = N'Approved');
GO

/* 4. Supporting indexes used by the new request/allocation queries. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tbl_RequestRoom_UserId_Status' AND object_id = OBJECT_ID(N'dbo.tbl_RequestRoom'))
    CREATE INDEX IX_tbl_RequestRoom_UserId_Status ON dbo.tbl_RequestRoom (UserId, Status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tbl_RequestRoom_ApprovedRoomId' AND object_id = OBJECT_ID(N'dbo.tbl_RequestRoom'))
    CREATE INDEX IX_tbl_RequestRoom_ApprovedRoomId ON dbo.tbl_RequestRoom (ApprovedRoomId) INCLUDE (Status);
GO

/* 5. Forced password change after an administrator resets a password. */
IF COL_LENGTH(N'dbo.tbl_User', N'MustChangePassword') IS NULL
    ALTER TABLE dbo.tbl_User ADD MustChangePassword BIT NOT NULL
        CONSTRAINT DF_tbl_User_MustChangePassword DEFAULT (0);
GO

/* 6. The room a student asks for when requesting (a preference; the administrator decides). */
IF COL_LENGTH(N'dbo.tbl_RequestRoom', N'PreferredRoomId') IS NULL
    ALTER TABLE dbo.tbl_RequestRoom ADD PreferredRoomId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_tbl_RequestRoom_PreferredRoom')
    ALTER TABLE dbo.tbl_RequestRoom ADD CONSTRAINT FK_tbl_RequestRoom_PreferredRoom
        FOREIGN KEY (PreferredRoomId) REFERENCES dbo.tbl_Room (Id);
GO

/* 7. Password hashes are longer now (79 characters instead of 48). An older database may have a narrower column,
      in which case new accounts could not be created and old hashes could not be upgraded at sign-in. */
DECLARE @passwordNullability NVARCHAR(10);
SELECT @passwordNullability = CASE WHEN is_nullable = 1 THEN N'NULL' ELSE N'NOT NULL' END
FROM   sys.columns
WHERE  object_id = OBJECT_ID(N'dbo.tbl_User') AND name = N'Password'
  AND  max_length <> -1 AND max_length < 512;   -- bytes: fewer than 256 characters
IF @passwordNullability IS NOT NULL
BEGIN
    DECLARE @widenPassword NVARCHAR(200) = N'ALTER TABLE dbo.tbl_User ALTER COLUMN [Password] NVARCHAR(256) ' + @passwordNullability + N';';
    EXEC sys.sp_executesql @widenPassword;
END
GO

/* 8. E-mailed password reset: the table that holds the (hashed) links. */
IF OBJECT_ID(N'dbo.tbl_PasswordReset', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tbl_PasswordReset
    (
        Id        INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_tbl_PasswordReset PRIMARY KEY,
        UserId    INT                NOT NULL,
        TokenHash CHAR(64)           NOT NULL,
        CreatedOn DATETIME           NOT NULL,
        ExpiresOn DATETIME           NOT NULL,
        UsedOn    DATETIME           NULL,
        CONSTRAINT UQ_tbl_PasswordReset_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_tbl_PasswordReset_User FOREIGN KEY (UserId) REFERENCES dbo.tbl_User (Id) ON DELETE CASCADE
    );
END
GO

/* ---------------------------------------------------------------------------------------------
   Diagnostics (read-only). Resolve any rows these return before relying on the new workflow.
   --------------------------------------------------------------------------------------------- */

/* Rooms allocated to more than one student at the same time. */
SELECT RR.ApprovedRoomId AS RoomId, COUNT(*) AS ActiveAllocations
FROM   dbo.tbl_RequestRoom RR
WHERE  RR.Status = N'Approved' AND RR.ApprovedRoomId IS NOT NULL
GROUP BY RR.ApprovedRoomId
HAVING COUNT(*) > 1;

/* Students holding more than one pending/approved request. */
SELECT RR.UserId, COUNT(*) AS ActiveRequests
FROM   dbo.tbl_RequestRoom RR
WHERE  RR.Status IN (N'Pending', N'Approved')
GROUP BY RR.UserId
HAVING COUNT(*) > 1;

/* Approved requests that point at no room (approved with an empty selection in the old version). */
SELECT RR.Id AS RequestId, RR.UserId
FROM   dbo.tbl_RequestRoom RR
WHERE  RR.Status = N'Approved' AND (RR.ApprovedRoomId IS NULL OR RR.ApprovedRoomId = 0);
GO
