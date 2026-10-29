// What a student does: register, sign in, ask for a room, manage the account. Real pages, browser, database.
const { describe, it, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const { launch, withSession, where, url, login, alertText, dismissAlert, gridRows, bodyText } = require('./support/browser');
const db = require('./support/db');

async function fillRegistration(page, v) {
  await page.goto(url('Register.aspx'));
  await page.fill('[id$="inputFirstName"]', v.first ?? 'Rita');
  await page.fill('[id$="inputLastName"]', v.last ?? 'Newcomer');
  await page.fill('[id$="inputEmail"]', v.email ?? 'rita@example.test');
  await page.fill('[id$="inputPassword"]', v.password ?? 'a good passphrase');
  await page.fill('[id$="inputPasswordConfirm"]', v.confirm ?? v.password ?? 'a good passphrase');
}

describe('student', () => {
  let browser;
  before(async () => {
    await db.clear();
    browser = await launch();
  });
  after(async () => { await browser.close(); });
  beforeEach(() => db.clear());

  describe('registration', () => {
    it('creates a student account that can sign in straight away', () =>
      withSession(browser, async ({ page }) => {
        await fillRegistration(page, {});
        await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
        assert.equal(await alertText(page), 'Your registration is successful. Please login.');
        await Promise.all([page.waitForNavigation(), dismissAlert(page)]);
        assert.equal(where(page), 'Login.aspx', 'closing the message goes to the login page');

        const [row] = await db.sql("SELECT UserType, FirstName, LastName, MobileNo, MustChangePassword, LEFT([Password], 3) AS prefix FROM dbo.tbl_User WHERE Email = N'rita@example.test';");
        assert.deepEqual(row, { UserType: 'Student', FirstName: 'Rita', LastName: 'Newcomer', MobileNo: '', MustChangePassword: false, prefix: 'v2$' });

        await login(page, 'rita@example.test', 'a good passphrase');
        assert.equal(where(page), 'UserDashboard.aspx');
        assert.match(await bodyText(page), /Rita Newcomer/);
      }));

    it('refuses an e-mail address that is already used, whatever its capitals', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'taken@example.test' });
        await fillRegistration(page, { email: 'TAKEN@example.test' });
        await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
        assert.equal(await alertText(page), 'This email already exists. Register with a different email address.');
        assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_User'))[0].n, 2); // the administrator and the existing student
      }));

    it('refuses a weak password with the reason', () =>
      withSession(browser, async ({ page }) => {
        await fillRegistration(page, { password: 'short' });
        await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
        assert.equal(await alertText(page), 'Choose a password of at least 8 characters.');
        await fillRegistration(page, { password: 'password123' });
        await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
        assert.equal(await alertText(page), 'That password is too easy to guess. Choose something less common.');
        assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_User'))[0].n, 1);
      }));

    it('points out a mismatched confirmation and a malformed e-mail address without creating anything', () =>
      withSession(browser, async ({ page }) => {
        await fillRegistration(page, { password: 'a good passphrase', confirm: 'something else' });
        await page.click('[id$="btnCreateAccount"]');
        await page.waitForSelector('text=Passwords do not match.');
        await fillRegistration(page, { email: 'not-an-email' });
        await page.click('[id$="btnCreateAccount"]');
        await page.waitForSelector('text=Please enter a valid email address.');
        assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_User'))[0].n, 1);
      }));

    it('does not accept HTML tags in the form (ASP.NET request validation) and stores nothing', () =>
      withSession(browser, async ({ page }) => {
        await fillRegistration(page, { first: '<b>Bold</b>', email: 'markup@example.test' });
        const [response] = await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateAccount"]')]);
        assert.equal(response.status(), 500, 'the request is refused');
        assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_User'))[0].n, 1, 'no account was created');
      }));
  });

  describe('dashboard', () => {
    it('starts empty and shows announcements that are current, newest first, as plain text', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test', first: 'Ann', last: 'Student' });
        await db.addAnnouncement({ title: 'Older notice', body: 'Line one\nLine two', ageMinutes: 60 });
        await db.addAnnouncement({ title: 'Newer <i>notice</i>', body: 'See <a href="http://evil.test">here</a>', ageMinutes: 5 });
        await db.addAnnouncement({ title: 'Ended notice', body: 'gone', ageMinutes: 600, expiresInDays: -1 });
        await login(page, 'ann@example.test', db.PASSWORD);

        const text = await bodyText(page);
        assert.match(text, /You have not made any room requests yet\./);
        assert.ok(text.indexOf('Newer <i>notice</i>') < text.indexOf('Older notice'), 'newest first');
        assert.ok(!text.includes('Ended notice'), 'an ended notice is hidden');
        assert.ok(text.includes('See <a href="http://evil.test">here</a>'), 'markup in a notice is shown as text');
        assert.equal(await page.locator('a[href="http://evil.test"]').count(), 0, 'and never becomes a link');
        assert.equal(await page.locator('.alert-info p br').count(), 1, 'line breaks in the body are kept');
      }));

    it('shows a name that contains markup as plain text (such data cannot come through the forms, but may exist)', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'markup@example.test', first: '<b>Bold</b>', last: "O'Brien & <i>Sons</i>" });
        await login(page, 'markup@example.test', db.PASSWORD);
        assert.ok((await bodyText(page)).includes("Logged in as: <b>Bold</b> O'Brien & <i>Sons</i>"));
        assert.equal(await page.locator('b:text-is("Bold")').count(), 0, 'no element was created from the name');
      }));

    it('has no announcements section when there is nothing to announce', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test' });
        await login(page, 'ann@example.test', db.PASSWORD);
        assert.ok(!(await bodyText(page)).includes('Announcements'));
      }));
  });

  describe('asking for a room', () => {
    it('sends a request with no preference, refuses a second one, and lets it be cancelled and asked again', () =>
      withSession(browser, async ({ page, dialogs }) => {
        await db.addUser({ email: 'ann@example.test' });
        await login(page, 'ann@example.test', db.PASSWORD);

        await Promise.all([page.waitForNavigation(), page.click('[id$="btnAddNewRequest"]')]);
        assert.equal(where(page), 'UserDashboard.aspx');
        let rows = await gridRows(page, 'gvRoomsRequest');
        assert.equal(rows.length, 1);
        assert.equal(rows[0][2], 'Pending');

        await Promise.all([page.waitForNavigation(), page.click('[id$="btnAddNewRequest"]')]);
        assert.equal(await alertText(page), 'You already have a pending request or an allocated room.');
        assert.equal((await gridRows(page, 'gvRoomsRequest')).length, 1);

        // A GET, not page.reload(): reloading a page that came from a POST would repeat the POST.
        await page.goto(url('UserDashboard.aspx'));
        await Promise.all([page.waitForNavigation(), page.click('a:has-text("Cancel request")')]);
        assert.deepEqual(dialogs, ['Cancel this request?']);
        rows = await gridRows(page, 'gvRoomsRequest');
        assert.equal(rows[0][2], 'Cancelled');
        assert.equal(await page.locator('a:has-text("Cancel request")').count(), 0, 'only pending requests offer cancel');

        await Promise.all([page.waitForNavigation(), page.click('[id$="btnAddNewRequest"]')]);
        rows = await gridRows(page, 'gvRoomsRequest');
        assert.deepEqual(rows.map(r => r[2]), ['Pending', 'Cancelled'], 'newest first');
      }));

    it('lists only free rooms and asks for the chosen one', () =>
      withSession(browser, async ({ page, dialogs }) => {
        const ann = await db.addUser({ email: 'ann@example.test' });
        const other = await db.addUser({ email: 'bob@example.test' });
        const wanted = await db.addRoom({ name: 'A-1 <b>sea</b> view', rent: 1250, description: 'Tom & Jerry <script>x</script>' });
        await db.addRoom({ name: 'B-2', rent: 800 });
        const taken = await db.addRoom({ name: 'Taken', rent: 100 });
        await db.allocate(taken, other);
        await login(page, 'ann@example.test', db.PASSWORD);

        await page.goto(url('AvailableRooms.aspx'));
        const rooms = await gridRows(page, 'gvRooms');
        assert.deepEqual(rooms.map(r => r.slice(0, 3)), [
          ['A-1 <b>sea</b> view', '1,250', 'Tom & Jerry <script>x</script>'],
          ['B-2', '800', 'A room'],
        ]);

        await Promise.all([page.waitForNavigation(), page.locator('tr', { hasText: 'A-1' }).locator('a:has-text("Request this room")').click()]);
        assert.deepEqual(dialogs, ['Send a request for this room?']);
        assert.equal(where(page), 'UserDashboard.aspx');
        assert.equal(await alertText(page), 'Your request has been sent. An administrator will review it.');
        const [row] = await gridRows(page, 'gvRoomsRequest');
        assert.equal(row[2], 'Pending');
        assert.equal(row[3], 'A-1 <b>sea</b> view', 'the preferred room is shown, as plain text');
        assert.equal(row[4], '', 'nothing is allocated yet');
        const [stored] = await db.sql('SELECT PreferredRoomId FROM dbo.tbl_RequestRoom WHERE UserId = @u', { u: ann });
        assert.equal(stored.PreferredRoomId, wanted);

        // The message is shown once.
        await page.reload();
        assert.equal(await page.locator('#modal-alert.show').count(), 0);
      }));

    it('says so when there are no rooms to offer', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test' });
        await login(page, 'ann@example.test', db.PASSWORD);
        await page.goto(url('AvailableRooms.aspx'));
        assert.match(await bodyText(page), /No rooms are available right now\. Please check again later\./);
      }));
  });

  describe('profile', () => {
    it('changes name and mobile number, and the new name appears in the navigation', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test', first: 'Ann', last: 'Student' });
        await login(page, 'ann@example.test', db.PASSWORD);
        await page.goto(url('MyProfile.aspx'));
        assert.equal(await page.inputValue('[id$="txtEmail"]'), 'ann@example.test');
        assert.equal(await page.isEditable('[id$="txtEmail"]'), false, 'the e-mail address cannot be edited here');

        await page.fill('[id$="txtFirstName"]', 'Anna');
        await page.fill('[id$="txtLastName"]', 'Renamed');
        await page.fill('[id$="txtMobile"]', '+44 7700 900123');
        await Promise.all([page.waitForNavigation(), page.click('[id$="btnSaveProfile"]')]);
        assert.equal(await alertText(page), 'Your profile has been updated.');
        assert.match(await bodyText(page), /Logged in as: Anna Renamed/);
        const [row] = await db.sql("SELECT FirstName, LastName, MobileNo FROM dbo.tbl_User WHERE Email = N'ann@example.test'");
        assert.deepEqual(row, { FirstName: 'Anna', LastName: 'Renamed', MobileNo: '+44 7700 900123' });
      }));

    it('rejects an invalid phone number and empty names before anything is sent', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test', mobile: '0700' });
        await login(page, 'ann@example.test', db.PASSWORD);
        await page.goto(url('MyProfile.aspx'));
        await page.fill('[id$="txtMobile"]', 'abc');
        await page.fill('[id$="txtFirstName"]', '');
        await page.click('[id$="btnSaveProfile"]');
        await page.waitForSelector('text=Enter a valid phone number.');
        await page.waitForSelector('text=Required');
        const [row] = await db.sql("SELECT MobileNo FROM dbo.tbl_User WHERE Email = N'ann@example.test'");
        assert.equal(row.MobileNo, '0700');
      }));
  });

  describe('password', () => {
    async function change(page, current, next, confirm = next) {
      await page.goto(url('MyProfile.aspx'));
      await page.fill('[id$="txtCurrentPassword"]', current);
      await page.fill('[id$="txtNewPassword"]', next);
      await page.fill('[id$="txtConfirmPassword"]', confirm);
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnChangePassword"]')]);
    }

    it('refuses a wrong current password, a weak new one, and an unchanged one', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test' });
        await login(page, 'ann@example.test', db.PASSWORD);
        await change(page, 'not my password', 'a fresh passphrase');
        assert.equal(await alertText(page), 'The current password is incorrect.');
        await change(page, db.PASSWORD, 'short');
        assert.equal(await alertText(page), 'Choose a password of at least 8 characters.');
        await change(page, db.PASSWORD, db.PASSWORD);
        assert.equal(await alertText(page), 'Choose a password that is different from your current one.');
      }));

    it('changes the password: the new one works, the old one does not', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test' });
        await login(page, 'ann@example.test', db.PASSWORD);
        await change(page, db.PASSWORD, 'a fresh passphrase');
        assert.equal(where(page), 'UserDashboard.aspx');
        assert.equal(await alertText(page), 'Your password has been changed.');

        await withSession(browser, async ({ page: again }) => {
          await login(again, 'ann@example.test', 'a fresh passphrase');
          assert.equal(where(again), 'UserDashboard.aspx');
        });
      }));

    it('after an administrator reset, the student can do nothing but choose a new password', () =>
      withSession(browser, async ({ page }) => {
        await db.addUser({ email: 'ann@example.test', mustChangePassword: true });
        await login(page, 'ann@example.test', db.PASSWORD);
        assert.equal(where(page), 'MyProfile.aspx');
        assert.match(await bodyText(page), /An administrator reset your password\. Please choose a new password below before you continue\./);
        for (const p of ['UserDashboard.aspx', 'AvailableRooms.aspx']) {
          await page.goto(url(p));
          assert.equal(where(page), 'MyProfile.aspx', `${p} is out of reach until the password is changed`);
        }
        await change(page, db.PASSWORD, 'a fresh passphrase');
        assert.equal(where(page), 'UserDashboard.aspx');
        await page.goto(url('AvailableRooms.aspx'));
        assert.equal(where(page), 'AvailableRooms.aspx', 'free again afterwards');
        const [row] = await db.sql("SELECT MustChangePassword FROM dbo.tbl_User WHERE Email = N'ann@example.test'");
        assert.equal(row.MustChangePassword, false);
      }));
  });

  describe('an account that disappears', () => {
    it('sends a signed-in student to the login page from the profile if an administrator has deleted the account', () =>
      withSession(browser, async ({ page }) => {
        const id = await db.addUser({ email: 'gone@example.test' });
        await login(page, 'gone@example.test', db.PASSWORD);
        await db.sql('DELETE FROM dbo.tbl_User WHERE Id = @id', { id });
        await page.goto(url('MyProfile.aspx'));
        assert.equal(where(page), 'Login.aspx');
      }));
  });
});
