// The database the end-to-end tests run against: built from the repository's own schema.sql, then filled directly with
// SQL so each test starts from a known state. Users made here share one password so tests can sign in as any of them.
const fs = require('fs');
const path = require('path');
const { DATABASE, repoRoot } = require('./config');
const { query, runScript } = require('./sql');

// Hash of PASSWORD in the application's current format, produced by the PowerShell snippet in Database/seed-admin.sql
// and confirmed with an independent PBKDF2 implementation (see SchemaScriptTests in the database tests).
const PASSWORD = 'seed-test-password';
const PASSWORD_HASH = 'v2$600000$3G0rZfLrB9ux/I53j90J2g==$V49VZfWYPbzuB2fjrfkd6rIxwEBnnDyttlYpe3xGpCI=';

const ADMIN_EMAIL = 'admin@example.com';

function retarget(script) {
  return script.replace(/\bHMSDB\b/g, DATABASE);
}

/**
 * Drops and recreates the database from Database/schema.sql. Run once, BEFORE the site starts (tests/e2e/run.sh does):
 * a running site keeps pooled connections to the old database, which a drop would break.
 */
async function create() {
  await query('master', `IF DB_ID(N'${DATABASE}') IS NOT NULL BEGIN ALTER DATABASE [${DATABASE}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${DATABASE}]; END`);
  await runScript('master', retarget(fs.readFileSync(path.join(repoRoot, 'Database', 'schema.sql'), 'utf8')));
  await addUser({ type: 'Admin', email: ADMIN_EMAIL, first: 'System', last: 'Administrator' });
}

/** Empties every table (children first) and recreates the first administrator, leaving the database itself in place. */
async function clear() {
  await query(DATABASE, 'DELETE FROM dbo.tbl_PasswordReset; DELETE FROM dbo.tbl_RequestRoom; DELETE FROM dbo.tbl_Announcement; DELETE FROM dbo.tbl_Room; DELETE FROM dbo.tbl_User;');
  await addUser({ type: 'Admin', email: ADMIN_EMAIL, first: 'System', last: 'Administrator' });
}

async function addUser({ type = 'Student', email, first = 'Sam', last = 'Student', mobile = '0700', mustChangePassword = false }) {
  const rows = await query(DATABASE,
    'INSERT INTO dbo.tbl_User (Email, [Password], UserType, FirstName, LastName, MobileNo, MustChangePassword, CreatedOn, ModifiedOn) ' +
    'OUTPUT INSERTED.Id AS id VALUES (@email, @hash, @type, @first, @last, @mobile, @must, GETDATE(), GETDATE());',
    { email, hash: PASSWORD_HASH, type, first, last, mobile, must: mustChangePassword });
  return rows[0].id;
}

async function addRoom({ name, rent = 500, description = 'A room', available = true }) {
  const rows = await query(DATABASE,
    'INSERT INTO dbo.tbl_Room (RoomName, Rent, [Description], Availability, CreatedOn, ModifiedOn) ' +
    'OUTPUT INSERTED.Id AS id VALUES (@name, @rent, @description, @available, GETDATE(), GETDATE());',
    { name, rent, description, available });
  return rows[0].id;
}

async function addRequest({ userId, status = 'Pending', roomId = null, preferredRoomId = null }) {
  const rows = await query(DATABASE,
    'INSERT INTO dbo.tbl_RequestRoom (UserId, ApprovedRoomId, PreferredRoomId, [Status], CreatedOn, RequestDate) ' +
    'OUTPUT INSERTED.Id AS id VALUES (@userId, @room, @preferred, @status, GETDATE(), GETDATE());',
    { userId, room: roomId, preferred: preferredRoomId, status });
  return rows[0].id;
}

/** A room a student holds: not available, with an Approved request pointing at it. */
async function allocate(roomId, userId) {
  await query(DATABASE, 'UPDATE dbo.tbl_Room SET Availability = 0 WHERE Id = @id;', { id: roomId });
  return addRequest({ userId, status: 'Approved', roomId });
}

/** `ageMinutes` is how long ago it was posted; `expiresInDays` is when it stops being shown (null: never). Times come from SQL Server. */
async function addAnnouncement({ title, body = 'Body', ageMinutes = 0, expiresInDays = null }) {
  const expires = expiresInDays === null ? 'NULL' : 'DATEADD(DAY, @days, GETDATE())';
  await query(DATABASE,
    `INSERT INTO dbo.tbl_Announcement (Title, Body, CreatedBy, CreatedOn, ExpiresOn) VALUES (@title, @body, 1, DATEADD(MINUTE, -@age, GETDATE()), ${expires});`,
    expiresInDays === null ? { title, body, age: ageMinutes } : { title, body, age: ageMinutes, days: expiresInDays });
}

const sql = (text, params) => query(DATABASE, text, params);

module.exports = { PASSWORD, ADMIN_EMAIL, create, clear, addUser, addRoom, addRequest, allocate, addAnnouncement, sql };
