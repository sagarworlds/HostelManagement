// Who can reach what, and how a session is protected. Real pages, real browser, real database.
const { describe, it, before, after } = require('node:test');
const assert = require('node:assert/strict');
const { launch, withSession, where, url, login, alertText, dismissAlert, logout } = require('./support/browser');
const { baseUrl } = require('./support/config');
const db = require('./support/db');
const { ANONYMOUS, ADMIN_ONLY, STUDENT_ONLY, ANY_SIGNED_IN, pagesOnDisk } = require('./support/pages');

// Failed sign-ins are rate limited per client address, and every test here is the same address. The app allows 30 per
// 15 minutes in total, so each failure below is counted: this file makes 8 (+ 5 for the lockout test).
describe('access control', () => {
  let browser;
  before(async () => {
    await db.clear();
    await db.addUser({ email: 'ann@example.test', first: 'Ann', last: 'Student' });
    browser = await launch();
  });
  after(async () => { await browser.close(); });

  it('every page of the site is classified in support/pages.js', () => {
    const listed = [...ANONYMOUS, ...ADMIN_ONLY, ...STUDENT_ONLY, ...ANY_SIGNED_IN].sort();
    assert.deepEqual(pagesOnDisk(), listed);
  });

  it('visitors who are not signed in are sent to the login page from every protected page', () =>
    withSession(browser, async ({ page }) => {
      for (const p of [...ADMIN_ONLY, ...STUDENT_ONLY, ...ANY_SIGNED_IN]) {
        await page.goto(url(p));
        assert.equal(where(page), 'Login.aspx', `${p} should send an anonymous visitor to the login page`);
      }
    }));

  it('the pages anyone may open load without a server error', () =>
    withSession(browser, async ({ page }) => {
      for (const p of ['Login.aspx', 'Register.aspx', 'ForgotPassword.aspx', 'ResetPassword.aspx']) {
        const response = await page.goto(url(p));
        assert.equal(response.status(), 200, p);
      }
      const error = await page.goto(url('Error.aspx'));
      assert.equal(error.status(), 500);
      assert.match(await page.title(), /Something went wrong/);
      const missing = await page.goto(url('Error.aspx?code=404'));
      assert.equal(missing.status(), 404);
    }));

  it('a student is sent away from every administrator page', () =>
    withSession(browser, async ({ page }) => {
      await login(page, 'ann@example.test', db.PASSWORD);
      assert.equal(where(page), 'UserDashboard.aspx');
      for (const p of ADMIN_ONLY) {
        await page.goto(url(p));
        assert.equal(where(page), 'Login.aspx', `${p} must not be open to a student`);
      }
    }));

  it('an administrator is sent away from the student-only pages', () =>
    withSession(browser, async ({ page }) => {
      await login(page, db.ADMIN_EMAIL, db.PASSWORD);
      assert.equal(where(page), 'adminDashboard.aspx');
      for (const p of STUDENT_ONLY) {
        await page.goto(url(p));
        assert.equal(where(page), 'Login.aspx', p);
      }
    }));

  it('the profile page opens for both kinds of account, inside their own navigation', async () => {
    await withSession(browser, async ({ page }) => {
      await login(page, db.ADMIN_EMAIL, db.PASSWORD);
      await page.goto(url('MyProfile.aspx'));
      assert.equal(where(page), 'MyProfile.aspx');
      assert.ok(await page.locator('a.nav-link[href="RoomList.aspx"]').count(), 'administrator navigation is shown');
    });
    await withSession(browser, async ({ page }) => {
      await login(page, 'ann@example.test', db.PASSWORD);
      await page.goto(url('MyProfile.aspx'));
      assert.equal(where(page), 'MyProfile.aspx');
      assert.equal(await page.locator('a.nav-link[href="RoomList.aspx"]').count(), 0, 'students do not see administrator navigation');
    });
  });

  it('a session id planted in another browser is useless without the cookie issued at sign-in', async () => {
    await withSession(browser, async victim => {
      await login(victim.page, 'ann@example.test', db.PASSWORD);
      const cookies = await victim.context.cookies();
      const sessionId = cookies.find(c => c.name === 'ASP.NET_SessionId');
      const binding = cookies.find(c => c.name === 'HostelHubAuth');
      assert.ok(sessionId && binding, 'sign-in issues both a session cookie and the binding cookie');
      assert.ok(sessionId.httpOnly && binding.httpOnly, 'both cookies are HttpOnly');

      // Control: another browser holding BOTH cookies is the same signed-in user.
      await withSession(browser, async same => {
        await same.context.addCookies([sessionId, binding].map(c => ({ name: c.name, value: c.value, url: baseUrl() })));
        await same.page.goto(url('UserDashboard.aspx'));
        assert.equal(where(same.page), 'UserDashboard.aspx');
      });

      // The attack: only the session id (as with a planted or stolen id) is not enough.
      await withSession(browser, async attacker => {
        await attacker.context.addCookies([{ name: sessionId.name, value: sessionId.value, url: baseUrl() }]);
        await attacker.page.goto(url('UserDashboard.aspx'));
        assert.equal(where(attacker.page), 'Login.aspx', 'the session id alone must not be enough');
      });

      // By design the session is ended when it is presented without its cookie (AccessGuard): the id may be known to
      // someone else, so the owner signs in again rather than keep using it.
      await victim.page.goto(url('UserDashboard.aspx'));
      assert.equal(where(victim.page), 'Login.aspx', 'the exposed session is terminated');
    });
  });

  it('signing out ends the session and the signed-in page is not served from a cache', () =>
    withSession(browser, async ({ page }) => {
      await login(page, 'ann@example.test', db.PASSWORD);
      const response = await page.goto(url('UserDashboard.aspx'));
      assert.match(response.headers()['cache-control'] || '', /no-store/, 'signed-in pages must not be stored');
      await logout(page);
      assert.equal(where(page), 'Login.aspx');
      await page.goto(url('UserDashboard.aspx'));
      assert.equal(where(page), 'Login.aspx');
    }));

  it('a wrong password and an unknown account get the same answer', () =>
    withSession(browser, async ({ page }) => {
      await login(page, 'ann@example.test', 'not the password');
      const wrongPassword = await alertText(page);
      await login(page, 'nobody@example.test', 'not the password');
      const unknownAccount = await alertText(page);
      assert.equal(wrongPassword, 'Email or Password is incorrect.');
      assert.equal(unknownAccount, wrongPassword);
      assert.equal(where(page), 'Login.aspx');
    }));

  it('repeated wrong passwords lock the account out, even for the right password', () =>
    withSession(browser, async ({ page }) => {
      await db.addUser({ email: 'lockme@example.test', first: 'Lock', last: 'Me' });
      for (let i = 0; i < 5; i++) {
        await login(page, 'lockme@example.test', 'wrong ' + i);
        assert.equal(await alertText(page), 'Email or Password is incorrect.');
      }
      await login(page, 'lockme@example.test', db.PASSWORD);
      assert.match(await alertText(page), /^Too many failed attempts\. Please try again in \d+ minutes?\.$/);
      assert.equal(where(page), 'Login.aspx', 'the correct password is refused while locked out');
    }));

  it('the forgotten-password link is hidden and the page explains why while no mail server is configured', () =>
    withSession(browser, async ({ page }) => {
      await page.goto(url('Login.aspx'));
      assert.equal(await page.locator('a[href$="ForgotPassword.aspx"]').count(), 0);
      await page.goto(url('ForgotPassword.aspx'));
      assert.match(await page.innerText('body'), /not set up on this system/);
      await page.goto(url('ResetPassword.aspx?token=made-up'));
      assert.match(await page.innerText('body'), /invalid or has expired/i);
    }));

  it('no page asks the browser for anything outside the site except the known libraries', () =>
    withSession(browser, async s => {
      await login(s.page, db.ADMIN_EMAIL, db.PASSWORD);
      for (const p of [...ADMIN_ONLY, ...ANONYMOUS]) await s.page.goto(url(p));
      assert.deepEqual(s.external, [], 'new external requests need a decision (and a line in support/browser.js)');
      assert.deepEqual(s.pageErrors, [], 'no script errors on any page');
    }));
});
