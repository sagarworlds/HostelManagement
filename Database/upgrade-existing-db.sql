/*
    HostelHub - bring an EXISTING HMSDB database in line with the current application.

    Safe to run more than once. It only touches rows/objects that need correcting, and finishes with
    read-only diagnostics for problems that need a human decision.

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
