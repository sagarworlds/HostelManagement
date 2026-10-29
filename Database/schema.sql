/*
    HostelHub - database schema (Microsoft SQL Server)

    IMPORTANT: this script was reconstructed from the SQL statements issued by the application
    (HostelManagement/Utility/*DB.cs). It was NOT exported from a live database, so column sizes and
    constraints are reasonable choices rather than a copy of an existing schema.

      * New installation  -> run this script, then seed-admin.sql.
      * Existing HMSDB    -> do not run this script; compare your tables with it and run
                             upgrade-existing-db.sql.

    The script is idempotent: every object is created only if it does not exist yet.
*/

IF DB_ID(N'HMSDB') IS NULL
    CREATE DATABASE HMSDB;
GO

USE HMSDB;
GO

/* ---------------------------------------------------------------------------------------------
   tbl_User - administrators and students.
   Password holds a self-describing PBKDF2 hash produced by PasswordUtility: "v2$<iterations>$<salt>$<hash>", 79
   characters today. Hashes written by earlier versions (48 characters) are upgraded when their owner signs in.
   MustChangePassword is set when an administrator resets the password; the user is then held on
   the profile page until they choose a new one.
   --------------------------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.tbl_User', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tbl_User
    (
        Id          INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_tbl_User PRIMARY KEY,
        Email       NVARCHAR(256)      NOT NULL,
        [Password]  NVARCHAR(256)      NOT NULL,
        UserType    NVARCHAR(20)       NOT NULL,
        FirstName   NVARCHAR(100)      NOT NULL,
        LastName    NVARCHAR(100)      NOT NULL,
        MobileNo    NVARCHAR(20)       NULL,
        MustChangePassword BIT         NOT NULL CONSTRAINT DF_tbl_User_MustChangePassword DEFAULT (0),
        CreatedOn   DATETIME           NOT NULL,
        ModifiedOn  DATETIME           NOT NULL,
        CONSTRAINT UQ_tbl_User_Email UNIQUE (Email),
        CONSTRAINT CK_tbl_User_UserType CHECK (UserType IN (N'Admin', N'Student'))
    );
END
GO

/* ---------------------------------------------------------------------------------------------
   tbl_Room - hostel rooms.
   Availability = 1 means the room can be allocated; approving a request sets it to 0 and
   vacating the room sets it back to 1. ModifiedBy is the tbl_User.Id of the last editor; it is
   deliberately not a foreign key so an administrator account can be deleted.
   --------------------------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.tbl_Room', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tbl_Room
    (
        Id            INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_tbl_Room PRIMARY KEY,
        RoomName      NVARCHAR(100)      NOT NULL,
        Rent          INT                NOT NULL,
        [Description] NVARCHAR(500)      NULL,
        Availability  BIT                NOT NULL CONSTRAINT DF_tbl_Room_Availability DEFAULT (1),
        ModifiedBy    INT                NULL,
        CreatedOn     DATETIME           NOT NULL,
        ModifiedOn    DATETIME           NOT NULL,
        CONSTRAINT CK_tbl_Room_Rent CHECK (Rent >= 0)
    );
END
GO

/* ---------------------------------------------------------------------------------------------
   tbl_RequestRoom - a student's room request and, once approved, the allocation.
   Status lifecycle:
       Pending   -> Approved | Rejected | Cancelled
       Approved  -> Vacated
   ApprovedRoomId is set while the request is Approved and kept as history after Vacated; it is
   cleared automatically if the room row is deleted (ON DELETE SET NULL).
   PreferredRoomId is the room the student asked for; it is only a preference, the administrator
   chooses the room. SQL Server does not allow a second cascading foreign key to the same table, so
   deleting a room clears this column in the application (RoomDB.DeleteRoom), inside one transaction.
   A student's requests must be deleted before the student row (the application does this inside
   one transaction), hence no cascade on UserId.
   --------------------------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.tbl_RequestRoom', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tbl_RequestRoom
    (
        Id             INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_tbl_RequestRoom PRIMARY KEY,
        UserId         INT                NOT NULL,
        ApprovedRoomId INT                NULL,
        PreferredRoomId INT               NULL,
        [Status]       NVARCHAR(20)       NOT NULL CONSTRAINT DF_tbl_RequestRoom_Status DEFAULT (N'Pending'),
        CreatedOn      DATETIME           NOT NULL,
        ModifiedOn     DATETIME           NULL,
        ModifiedBy     INT                NULL,
        RequestDate    DATETIME           NOT NULL,
        ResponseDate   DATETIME           NULL,
        CONSTRAINT FK_tbl_RequestRoom_User FOREIGN KEY (UserId) REFERENCES dbo.tbl_User (Id),
        CONSTRAINT FK_tbl_RequestRoom_Room FOREIGN KEY (ApprovedRoomId) REFERENCES dbo.tbl_Room (Id) ON DELETE SET NULL,
        CONSTRAINT FK_tbl_RequestRoom_PreferredRoom FOREIGN KEY (PreferredRoomId) REFERENCES dbo.tbl_Room (Id),
        CONSTRAINT CK_tbl_RequestRoom_Status CHECK ([Status] IN (N'Pending', N'Approved', N'Rejected', N'Cancelled', N'Vacated'))
    );
END
GO

/* Supports "does this student already have an active request" and the per-room allocation look-ups. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tbl_RequestRoom_UserId_Status' AND object_id = OBJECT_ID(N'dbo.tbl_RequestRoom'))
    CREATE INDEX IX_tbl_RequestRoom_UserId_Status ON dbo.tbl_RequestRoom (UserId, [Status]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tbl_RequestRoom_ApprovedRoomId' AND object_id = OBJECT_ID(N'dbo.tbl_RequestRoom'))
    CREATE INDEX IX_tbl_RequestRoom_ApprovedRoomId ON dbo.tbl_RequestRoom (ApprovedRoomId) INCLUDE ([Status]);
GO
