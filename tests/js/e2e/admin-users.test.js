// The administrator's people management: students, other administrators, and announcements.
const { describe, it, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const { launch, withSession, where, url, login, alertText, dismissAlert, gridRows, bodyText } = require('./support/browser');
const db = require('./support/db');

const asAdmin = (browser, body) => withSession(browser, async s => { await login(s.page, db.ADMIN_EMAIL, db.PASSWORD); return body(s); });

describe('administrator: students', () => {
  let browser;
  before(async () => { await db.clear(); browser = await launch(); });
  after(async () => { await browser.close(); });
  beforeEach(() => db.clear());

  it('lists the students, without an Add button, and shows names as plain text', async () => {
    await db.addUser({ email: 'zed@example.test', first: 'Zed', last: 'Last' });
    await db.addUser({ email: 'amy@example.test', first: '<b>Amy</b>', last: 'First' });

    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('AdminList.aspx?UserType=Student'));
      assert.match(await bodyText(page), /User List/);
      assert.equal(await page.locator('a:has-text("Add Admin")').count(), 0, 'students register themselves');
      assert.deepEqual((await gridRows(page, 'gvAdmin')).map(r => r.slice(0, 2)), [
        ['<b>Amy</b> First', 'amy@example.test'],
        ['Zed Last', 'zed@example.test'],
      ]);
    });
  });

  it('edits a student: name and e-mail change, the password does not, and a duplicate e-mail is refused', async () => {
    const ann = await db.addUser({ email: 'ann@example.test', first: 'Ann', last: 'Student' });
    await db.addUser({ email: 'taken@example.test' });
    const [before] = await db.sql('SELECT [Password] AS hash FROM dbo.tbl_User WHERE Id = @id', { id: ann });

    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('AdminList.aspx?UserType=Student'));
      await Promise.all([page.waitForNavigation(), page.click(`a[href="AddEditAdmin.aspx?UserType=Student&Id=${ann}"]`)]);
      assert.match(await bodyText(page), /Edit User/);
      assert.equal(await page.inputValue('[id$="inputEmail"]'), 'ann@example.test');
      assert.equal(await page.locator('[id$="inputPassword"]').count(), 0, 'passwords are not edited here');

      await page.fill('[id$="inputEmail"]', 'TAKEN@example.test');
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
      assert.equal(await alertText(page), 'This email already exists. Use a different email address.');
      await dismissAlert(page);

      await page.fill('[id$="inputFirstName"]', 'Anna');
      await page.fill('[id$="inputEmail"]', 'anna@example.test');
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
      assert.equal(where(page), 'AdminList.aspx?UserType=Student');
      assert.deepEqual((await gridRows(page, 'gvAdmin')).map(r => r[1]), ['anna@example.test', 'taken@example.test']);
    });
    const [after] = await db.sql('SELECT [Password] AS hash, UserType FROM dbo.tbl_User WHERE Id = @id', { id: ann });
    assert.equal(after.hash, before.hash);
    assert.equal(after.UserType, 'Student');
  });

  it('does not let an invented account id or the wrong type open the edit form', async () => {
    const ann = await db.addUser({ email: 'ann@example.test' });
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('AddEditAdmin.aspx?UserType=Student&Id=987654'));
      assert.equal(where(page), 'AdminList.aspx?UserType=Student');
      assert.equal(await alertText(page), 'That account no longer exists.');
      await page.goto(url(`AddEditAdmin.aspx?UserType=Admin&Id=${ann}`));   // a student's id through the admin form
      assert.equal(await alertText(page), 'That account no longer exists.');
      await page.goto(url('AddEditAdmin.aspx?UserType=Student'));           // adding a student here is not possible
      assert.equal(where(page), 'AdminList.aspx?UserType=Student');
    });
  });

  it('resets a student\'s password: a temporary one is shown once, and the student must replace it', async () => {
    await db.addUser({ email: 'ann@example.test', first: 'Ann', last: 'Student' });

    let temporary;
    await asAdmin(browser, async ({ page, dialogs }) => {
      await page.goto(url('AdminList.aspx?UserType=Student'));
      await Promise.all([page.waitForNavigation(), page.click('a[title="Reset password"]')]);
      assert.deepEqual(dialogs, ['Reset the password for this student? A temporary password will be generated.']);
      const message = await alertText(page);
      const found = /^Temporary password for Ann Student: (\S+)\s+-\s+give it to the student\. They must choose a new password when they sign in\. It will not be shown again\.$/.exec(message);
      assert.ok(found, 'unexpected message: ' + message);
      temporary = found[1];
      assert.ok(temporary.length >= 8);
      await page.goto(url('AdminList.aspx?UserType=Student'));   // a GET: reloading would repeat the reset
      assert.equal(await page.locator('#modal-alert.show').count(), 0);
    });

    await withSession(browser, async ({ page }) => {
      await login(page, 'ann@example.test', db.PASSWORD);
      await page.waitForSelector('#modal-alert.show');         // the old password no longer works
      assert.equal(where(page), 'Login.aspx');
    });
    await withSession(browser, async ({ page }) => {
      await login(page, 'ann@example.test', temporary);
      assert.equal(where(page), 'MyProfile.aspx', 'a reset student is held on the profile page');
      await page.goto(url('UserDashboard.aspx'));
      assert.equal(where(page), 'MyProfile.aspx');
    });
  });

  it('deletes a student with their requests, and the room they held becomes available', async () => {
    const ann = await db.addUser({ email: 'ann@example.test' });
    await db.addUser({ email: 'bob@example.test', first: 'Bob', last: 'Other' });
    const room = await db.addRoom({ name: 'Held' });
    await db.allocate(room, ann);
    await db.addRequest({ userId: ann, status: 'Rejected' });

    await asAdmin(browser, async ({ page, dialogs }) => {
      await page.goto(url('AdminList.aspx?UserType=Student'));
      await Promise.all([page.waitForNavigation(), page.locator('tr', { hasText: 'ann@example.test' }).locator('a[title="Delete"]').click()]);
      assert.deepEqual(dialogs, ['Are you sure you want to delete this']);
      assert.deepEqual((await gridRows(page, 'gvAdmin')).map(r => r[1]), ['bob@example.test']);
    });
    assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_RequestRoom'))[0].n, 0);
    assert.equal((await db.sql('SELECT Availability FROM dbo.tbl_Room WHERE Id = @id', { id: room }))[0].Availability, true);
  });
});

describe('administrator: administrators', () => {
  let browser;
  before(async () => { await db.clear(); browser = await launch(); });
  after(async () => { await browser.close(); });
  beforeEach(() => db.clear());

  async function fill(page, v) {
    await page.fill('[id$="inputFirstName"]', v.first ?? 'New');
    await page.fill('[id$="inputLastName"]', v.last ?? 'Admin');
    await page.fill('[id$="inputEmail"]', v.email ?? 'new.admin@example.test');
    await page.fill('[id$="inputPassword"]', v.password ?? 'a good passphrase');
    await page.fill('[id$="inputPasswordConfirm"]', v.confirm ?? v.password ?? 'a good passphrase');
  }

  it('is the default list, falls back to it for an unknown type, and offers Add Admin', async () => {
    await asAdmin(browser, async ({ page }) => {
      for (const p of ['AdminList.aspx', 'AdminList.aspx?UserType=Hacker', 'AdminList.aspx?UserType=Admin']) {
        await page.goto(url(p));
        assert.match(await bodyText(page), /Admin List/, p);
        assert.equal(await page.locator('a:has-text("Add Admin")').count(), 1, p);
        assert.deepEqual((await gridRows(page, 'gvAdmin')).map(r => r[1]), [db.ADMIN_EMAIL], p);
      }
    });
  });

  it('adds an administrator who can then sign in to the administrator area', async () => {
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('AdminList.aspx'));
      await Promise.all([page.waitForNavigation(), page.click('a:has-text("Add Admin")')]);
      await fill(page, {});
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
      assert.equal(where(page), 'AdminList.aspx?UserType=Admin');
      assert.deepEqual((await gridRows(page, 'gvAdmin')).map(r => r[1]).sort(), [db.ADMIN_EMAIL, 'new.admin@example.test'].sort());
    });
    await withSession(browser, async ({ page }) => {
      await login(page, 'new.admin@example.test', 'a good passphrase');
      assert.equal(where(page), 'adminDashboard.aspx');
    });
  });

  it('refuses a weak password, a mismatch, and an e-mail address already in use', async () => {
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('AddEditAdmin.aspx'));
      await fill(page, { password: 'short' });
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
      assert.equal(await alertText(page), 'Choose a password of at least 8 characters.');

      await page.goto(url('AddEditAdmin.aspx'));
      await fill(page, { password: 'a good passphrase', confirm: 'another one' });
      await page.click('[id$="btnCreateAccount"]');
      await page.waitForSelector('text=Passwords do not match.');

      await page.goto(url('AddEditAdmin.aspx'));
      await fill(page, { email: db.ADMIN_EMAIL.toUpperCase() });
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
      assert.equal(await alertText(page), 'This email already exists. Use a different email address.');
    });
    assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_User'))[0].n, 1);
  });

  it('edits an administrator, including themselves, and the sidebar name follows', async () => {
    const me = (await db.sql('SELECT Id FROM dbo.tbl_User WHERE Email = @e', { e: db.ADMIN_EMAIL }))[0].Id;
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url(`AddEditAdmin.aspx?UserType=Admin&Id=${me}`));
      assert.match(await bodyText(page), /Edit Admin/);
      await page.fill('[id$="inputFirstName"]', 'Renamed');
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
      assert.match(await bodyText(page), /Logged in as: Renamed Administrator/);
    });
  });

  it('will not delete the account you are signed in with, but deletes another administrator', async () => {
    await db.addUser({ type: 'Admin', email: 'other@example.test', first: 'Other', last: 'Admin' });
    await asAdmin(browser, async ({ page, dialogs }) => {
      await page.goto(url('AdminList.aspx'));
      await Promise.all([page.waitForNavigation(), page.locator('tr', { hasText: db.ADMIN_EMAIL }).locator('a[title="Delete"]').click()]);
      assert.equal(await alertText(page), 'You cannot delete the account you are signed in with.');
      assert.equal((await gridRows(page, 'gvAdmin')).length, 2);

      await page.goto(url('AdminList.aspx'));
      await Promise.all([page.waitForNavigation(), page.locator('tr', { hasText: 'other@example.test' }).locator('a[title="Delete"]').click()]);
      assert.equal(dialogs.length, 2);
      assert.deepEqual((await gridRows(page, 'gvAdmin')).map(r => r[1]), [db.ADMIN_EMAIL]);
    });
  });

  it('keeps an administrator out of the list that was deleted while they were signed in', async () => {
    const other = await db.addUser({ type: 'Admin', email: 'other@example.test' });
    await withSession(browser, async ({ page }) => {
      await login(page, 'other@example.test', db.PASSWORD);
      await db.sql('DELETE FROM dbo.tbl_User WHERE Id = @id', { id: other });
      await page.goto(url('MyProfile.aspx'));
      assert.equal(where(page), 'Login.aspx', 'a deleted account cannot carry on with its old session');
    });
  });
});

describe('administrator: announcements', () => {
  let browser;
  before(async () => { await db.clear(); browser = await launch(); });
  after(async () => { await browser.close(); });
  beforeEach(() => db.clear());

  async function post(page, { title, body, lastDay }) {
    await page.goto(url('Announcements.aspx'));
    await page.fill('[id$="txtTitle"]', title ?? 'Water off');
    await page.fill('[id$="txtBody"]', body ?? 'No water on Tuesday morning.');
    if (lastDay !== undefined) await page.fill('[id$="txtLastDay"]', lastDay);
    await Promise.all([page.waitForNavigation(), page.click('[id$="btnPost"]')]);
  }

  const dayFromNow = n => { const d = new Date(Date.now() + n * 86400000); return d.toISOString().slice(0, 10); };

  it('posts an announcement that students then see, and removes it again', async () => {
    await db.addUser({ email: 'ann@example.test' });
    await asAdmin(browser, async ({ page, dialogs }) => {
      await post(page, { title: 'Water off', body: 'No water on Tuesday.' });
      assert.equal(await alertText(page), 'Announcement posted.');
      assert.deepEqual((await gridRows(page, 'gvAnnouncements')).map(r => [r[0], r[2]]), [['Water off', 'Shown until removed']]);

      await withSession(browser, async student => {
        await login(student.page, 'ann@example.test', db.PASSWORD);
        assert.match(await bodyText(student.page), /Water off.*No water on Tuesday\./);
      });

      await page.goto(url('Announcements.aspx'));
      await Promise.all([page.waitForNavigation(), page.click('a[title="Delete"]')]);
      assert.deepEqual(dialogs, ['Remove this announcement?']);
      assert.match(await bodyText(page), /No announcements have been posted yet\./);
    });
  });

  it('can end on a chosen day, and refuses a day that has passed', async () => {
    await asAdmin(browser, async ({ page }) => {
      const last = dayFromNow(3);
      await post(page, { title: 'Until later', lastDay: last });
      assert.equal(await alertText(page), 'Announcement posted.');
      const [status] = (await gridRows(page, 'gvAnnouncements')).map(r => r[2]);
      assert.match(status, /^Shown until \d{2} [A-Z][a-z]{2} \d{4}$/);

      await post(page, { title: 'Too late', lastDay: dayFromNow(-2) });
      assert.equal(await alertText(page), 'The last day cannot be in the past.');
    });
    assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_Announcement'))[0].n, 1);
  });

  it('needs a title and a message, and does not accept HTML tags', async () => {
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('Announcements.aspx'));
      await page.click('[id$="btnPost"]');
      await page.waitForSelector('text=Required');

      await page.goto(url('Announcements.aspx'));
      await page.fill('[id$="txtTitle"]', '<script>alert(1)</script>');
      await page.fill('[id$="txtBody"]', 'x');
      const [response] = await Promise.all([page.waitForNavigation(), page.click('[id$="btnPost"]')]);
      assert.equal(response.status(), 500, 'ASP.NET request validation refuses it');
    });
    assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_Announcement'))[0].n, 0);
  });
});
