// What a person who runs the site relies on: the health address that monitors call, and what the application writes to its
// log when it starts. Real handler, real log files, real database.
const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const { baseUrl, DATABASE } = require('./support/config');
const { query } = require('./support/sql');
const { PLANTED_OLD_LOG, logFolder, readLog } = require('./support/logs');

const health = () => new URL('Health.ashx', baseUrl());
// redirect: 'manual' so that a redirect to the login page would show up as a failure instead of being followed.
const ask = () => fetch(health(), { redirect: 'manual' });

describe('operations', () => {
  it('the health address answers Healthy to anyone, without a session, and is never cached', async () => {
    const response = await ask();
    assert.equal(response.status, 200);
    assert.equal(await response.text(), 'Healthy');
    assert.match(response.headers.get('content-type'), /^text\/plain/);
    assert.match(response.headers.get('cache-control'), /no-store|no-cache/);
    assert.equal(response.headers.get('set-cookie'), null, 'a monitor that calls it every few seconds must not create sessions');
  });

  it('it answers 503 and nothing more while the database lacks a table, and recovers when the table is back', async () => {
    await query(DATABASE, "EXEC sp_rename N'dbo.tbl_Announcement', N'tbl_Announcement_hidden';");
    try {
      const response = await ask();
      assert.equal(response.status, 503);
      const body = await response.text();
      assert.equal(body, 'Unhealthy');   // the reason is for the log, not for a stranger
      assert.doesNotMatch(body, /tbl_|Announcement/);
    } finally {
      await query(DATABASE, "EXEC sp_rename N'dbo.tbl_Announcement_hidden', N'tbl_Announcement';");
    }
    assert.equal((await ask()).status, 200);
  });

  it('the application checked its database when it started, and deleted an old log file but not a stranger\'s file', async () => {
    // tests/e2e/run.sh planted both before the site started. The site itself starts on its first request.
    await ask();
    const files = fs.readdirSync(logFolder());
    const today = readLog();

    assert.match(today, /Startup: the database has every table and column this version of the application needs\./);
    assert.ok(!files.includes(PLANTED_OLD_LOG), 'a daily log older than the retention period is deleted');
    assert.match(today, /Deleted 1 log file\(s\) older than 90 days\./);
    assert.ok(files.includes('notes.txt'), 'a file that is not one of the logger\'s own is left alone');
  });
});
