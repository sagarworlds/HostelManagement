// Quality of what every page sends to the browser: a title, labels that belong to something, and fields that have a name.
// Each check visits every page as the right kind of user and reports ALL offenders at once.
const { describe, it, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const { launch, withSession, url, login } = require('./support/browser');
const db = require('./support/db');

// Pages are visited as the role that may open them. Pages that need data in the address get it from the fixtures below.
async function visitAll(browser, inspect) {
  const found = [];
  const record = (page, problems) => problems.forEach(p => found.push(`${page}: ${p}`));
  const fixture = {
    room: await db.addRoom({ name: 'Fixture room' }),
    student: await db.addUser({ email: 'ann@example.test' }),
  };
  const request = await db.addRequest({ userId: fixture.student });

  await withSession(browser, async s => {                       // not signed in
    for (const p of ['Login.aspx', 'Register.aspx', 'ForgotPassword.aspx', 'ResetPassword.aspx?token=x']) {
      await s.page.goto(url(p));
      record(p, await inspect(s.page));
    }
  });
  await withSession(browser, async s => {                       // administrator
    await login(s.page, db.ADMIN_EMAIL, db.PASSWORD);
    for (const p of ['adminDashboard.aspx', 'RoomList.aspx', 'AddEditRoom.aspx', `AddEditRoom.aspx?Id=${fixture.room}`,
      'AdminList.aspx', 'AdminList.aspx?UserType=Student', 'AddEditAdmin.aspx', `AddEditAdmin.aspx?UserType=Student&Id=${fixture.student}`,
      'Announcements.aspx', `ApproveRoomRequest.aspx?Id=${request}`, 'MyProfile.aspx']) {
      await s.page.goto(url(p));
      record(p + ' (administrator)', await inspect(s.page));
    }
  });
  await withSession(browser, async s => {                       // student
    await login(s.page, 'ann@example.test', db.PASSWORD);
    for (const p of ['UserDashboard.aspx', 'AvailableRooms.aspx', 'MyProfile.aspx']) {
      await s.page.goto(url(p));
      record(p + ' (student)', await inspect(s.page));
    }
  });
  return found;
}

describe('page markup', () => {
  let browser;
  before(async () => { await db.clear(); browser = await launch(); });
  after(async () => { await browser.close(); });
  beforeEach(() => db.clear());

  it('every page has a title that says where you are', async () => {
    const found = await visitAll(browser, async page => ((await page.title()).trim() === '' ? ['no title'] : []));
    assert.deepEqual(found, []);
  });

  it('every label belongs to a field that exists', async () => {
    const found = await visitAll(browser, page => page.evaluate(() =>
      [...document.querySelectorAll('label[for]')]
        .filter(l => !document.getElementById(l.htmlFor))
        .map(l => `label "${l.innerText.trim()}" is for "${l.htmlFor}", which does not exist`)));
    assert.deepEqual(found, []);
  });

  it('every field a person fills in has a name (a label or aria-label)', async () => {
    const found = await visitAll(browser, page => page.evaluate(() =>
      [...document.querySelectorAll('input, select, textarea')]
        .filter(e => !['hidden', 'submit', 'button', 'image', 'checkbox', 'radio'].includes(e.type))
        .filter(e => !e.closest('.datatable-container'))        // the grid search box comes from the library
        .filter(e => !(e.labels && e.labels.length) && !e.getAttribute('aria-label') && !e.getAttribute('aria-labelledby'))
        .map(e => `${e.tagName.toLowerCase()}#${e.id} has no label`)));
    assert.deepEqual(found, []);
  });
});
