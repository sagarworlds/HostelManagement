/*
    The database the first version of HostelHub ran on, as far as that version's own SQL shows (git commit ea4454e:
    it read and wrote only the columns below, and no database script shipped with it). Nothing here guards the data:
    no foreign keys, no unique e-mail, no defaults, no indexes, a nullable Availability, and a Password column sized
    for the 48-character hashes of that time.

    Used by UpgradeScriptTests; it is deliberately NOT what schema.sql builds.
*/
CREATE DATABASE HMSDB;
GO

USE HMSDB;
GO

CREATE TABLE dbo.tbl_User
(
    Id         INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    Email      NVARCHAR(100)      NOT NULL,
    [Password] VARCHAR(50)        NOT NULL,
    UserType   NVARCHAR(20)       NOT NULL,
    FirstName  NVARCHAR(50)       NOT NULL,
    LastName   NVARCHAR(50)       NOT NULL,
    MobileNo   NVARCHAR(20)       NULL,
    CreatedOn  DATETIME           NULL,
    ModifiedOn DATETIME           NULL
);
GO

CREATE TABLE dbo.tbl_Room
(
    Id            INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    RoomName      NVARCHAR(50)       NOT NULL,
    Rent          INT                NOT NULL,
    [Description] NVARCHAR(200)      NULL,
    Availability  BIT                NULL,
    ModifiedBy    INT                NULL,
    CreatedOn     DATETIME           NULL,
    ModifiedOn    DATETIME           NULL
);
GO

CREATE TABLE dbo.tbl_RequestRoom
(
    Id             INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    UserId         INT                NOT NULL,
    ApprovedRoomId INT                NULL,
    [Status]       NVARCHAR(20)       NULL,
    CreatedOn      DATETIME           NULL,
    ModifiedOn     DATETIME           NULL,
    ModifiedBy     INT                NULL,
    RequestDate    DATETIME           NULL,
    ResponseDate   DATETIME           NULL
);
GO
