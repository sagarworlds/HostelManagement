// The administrator's room management: list, add, edit, delete, vacate.
const { describe, it, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const { launch, withSession, where, url, login, alertText, gridRows, bodyText } = require('./support/browser');
const db = require('./support/db');

const asAdmin = (browser, body) => withSession(browser, async s => { await login(s.page, db.ADMIN_EMAIL, db.PASSWORD); return body(s); });

async function saveRoom(page, { name, rent, description }) {
  if (name !== undefined) await page.fill('[id$="txtRoomName"]', name);
  if (rent !== undefined) await page.fill('[id$="txtRent"]', String(rent));
  if (description !== undefined) await page.fill('[id$="txtDescription"]', description);
  await Promise.all([page.waitForNavigation(), page.click('[id$="btnCreateRoom"]')]);
}

describe('administrator: rooms', () => {
  let browser;
  before(async () => { await db.clear(); browser = await launch(); });
  after(async () => { await browser.close(); });
  beforeEach(() => db.clear());

  it('lists every room with who holds it, and shows names and descriptions as plain text', async () => {
    const ann = await db.addUser({ email: 'ann@example.test', first: 'Ann', last: 'Student' });
    const held = await db.addRoom({ name: 'B-2 <b>held</b>', rent: 700, description: 'Tom & <i>Jerry</i>' });
    await db.addRoom({ name: 'A-1', rent: 500 });
    await db.addRoom({ name: 'C-3', rent: 300, available: false });
    await db.allocate(held, ann);

    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('RoomList.aspx'));
      const rows = await gridRows(page, 'gvRooms');
      assert.deepEqual(rows.map(r => r.slice(0, 4)), [
        ['A-1', '500', 'A room', 'Available'],
        ['B-2 <b>held</b>', '700', 'Tom & <i>Jerry</i>', 'Occupied by Ann Student'],
        ['C-3', '300', 'A room', 'Unavailable'],
      ]);
      assert.equal(await page.locator('b:text-is("held")').count(), 0);
    });
  });

  it('adds a room that students can then be offered', async () => {
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('RoomList.aspx'));
      await Promise.all([page.waitForNavigation(), page.click('a:has-text("Add New")')]);
      assert.equal(where(page), 'AddEditRoom.aspx');
      await saveRoom(page, { name: 'D-4', rent: 650, description: 'Quiet corner' });
      assert.equal(where(page), 'RoomList.aspx');
      assert.deepEqual((await gridRows(page, 'gvRooms')).map(r => r.slice(0, 4)), [['D-4', '650', 'Quiet corner', 'Available']]);
    });
    const [room] = await db.sql("SELECT Availability, ModifiedBy FROM dbo.tbl_Room WHERE RoomName = N'D-4'");
    assert.equal(room.Availability, true);
    assert.ok(room.ModifiedBy > 0, 'the administrator who created it is recorded');
  });

  it('refuses a room without a name, with a negative rent, or with too long a description', async () => {
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('AddEditRoom.aspx'));
      await page.click('[id$="btnCreateRoom"]');
      await page.waitForSelector('text=Required');

      await page.goto(url('AddEditRoom.aspx'));
      await saveRoom(page, { name: 'Bad', rent: -5 });
      assert.equal(await alertText(page), 'Enter the rent as a whole number, 0 or more.');

      await page.goto(url('AddEditRoom.aspx'));
      await saveRoom(page, { name: 'Wordy', rent: 100, description: 'x'.repeat(501) });
      assert.equal(await alertText(page), 'The description can be at most 500 characters.');
    });
    assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_Room'))[0].n, 0);
  });

  it('edits a room, starting from its current values, and says so for a room that no longer exists', async () => {
    const id = await db.addRoom({ name: 'Old name', rent: 400, description: 'Old text' });
    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('RoomList.aspx'));
      await Promise.all([page.waitForNavigation(), page.click(`a[href="AddEditRoom.aspx?Id=${id}"]`)]);
      assert.equal(await page.inputValue('[id$="txtRoomName"]'), 'Old name');
      assert.equal(await page.inputValue('[id$="txtRent"]'), '400');
      assert.equal(await page.inputValue('[id$="btnCreateRoom"]'), 'Update Room');
      await saveRoom(page, { name: 'New name', rent: 450, description: 'New text' });
      assert.deepEqual((await gridRows(page, 'gvRooms')).map(r => r.slice(0, 4)), [['New name', '450', 'New text', 'Available']]);

      await page.goto(url('AddEditRoom.aspx?Id=987654'));
      assert.equal(where(page), 'RoomList.aspx');
      assert.equal(await alertText(page), 'That room no longer exists.');
    });
  });

  it('deletes a free room after asking, and does not offer deleting one a student holds', async () => {
    const ann = await db.addUser({ email: 'ann@example.test' });
    const free = await db.addRoom({ name: 'Free' });
    const held = await db.addRoom({ name: 'Held' });
    await db.allocate(held, ann);

    await asAdmin(browser, async ({ page, dialogs }) => {
      await page.goto(url('RoomList.aspx'));
      assert.equal(await page.locator('tr', { hasText: 'Held' }).locator('a[title="Delete"]').count(), 0, 'no delete link for an occupied room');
      await Promise.all([page.waitForNavigation(), page.locator('tr', { hasText: 'Free' }).locator('a[title="Delete"]').click()]);
      assert.deepEqual(dialogs, ['Are you sure you want to delete this']);
      assert.deepEqual((await gridRows(page, 'gvRooms')).map(r => r[0]), ['Held']);
    });
    assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_Room WHERE Id = @id', { id: free }))[0].n, 0);
  });

  it('refuses to delete a room that a student was given while the list was open', async () => {
    const ann = await db.addUser({ email: 'ann@example.test' });
    const room = await db.addRoom({ name: 'Just taken' });

    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('RoomList.aspx'));
      await db.allocate(room, ann);   // another administrator approves a request into it
      await Promise.all([page.waitForNavigation(), page.locator('a[title="Delete"]').click()]);
      assert.equal(await alertText(page), 'This room is currently allocated to a student. Vacate it before deleting it.');
      assert.deepEqual((await gridRows(page, 'gvRooms')).map(r => r[3]), ['Occupied by Sam Student']);
    });
    assert.equal((await db.sql('SELECT COUNT(1) AS n FROM dbo.tbl_Room WHERE Id = @id', { id: room }))[0].n, 1);
  });

  it('vacates a room: the student no longer holds it and it can be offered again', async () => {
    const ann = await db.addUser({ email: 'ann@example.test' });
    const room = await db.addRoom({ name: 'Leaving' });
    const request = await db.allocate(room, ann);

    await asAdmin(browser, async ({ page, dialogs }) => {
      await page.goto(url('RoomList.aspx'));
      assert.equal(await page.locator('a[title*="acate"]').count(), 1, 'only an occupied room offers vacating');
      await Promise.all([page.waitForNavigation(), page.locator('a[title*="acate"]').click()]);
      assert.deepEqual(dialogs, ['Vacate this room? The student will no longer hold it.']);
      assert.deepEqual((await gridRows(page, 'gvRooms')).map(r => r[3]), ['Available']);
      assert.equal(await page.locator('a[title*="acate"]').count(), 0);
    });
    assert.equal((await db.sql('SELECT Status FROM dbo.tbl_RequestRoom WHERE Id = @id', { id: request }))[0].Status, 'Vacated');

    await withSession(browser, async ({ page }) => {
      await login(page, 'ann@example.test', db.PASSWORD);
      assert.equal((await gridRows(page, 'gvRoomsRequest'))[0][2], 'Vacated');
      await Promise.all([page.waitForNavigation(), page.click('[id$="btnAddNewRequest"]')]);
      assert.deepEqual((await gridRows(page, 'gvRoomsRequest')).map(r => r[2]), ['Pending', 'Vacated'], 'the student can ask again');
    });
  });

  it('says so when a room was vacated by someone else first', async () => {
    const ann = await db.addUser({ email: 'ann@example.test' });
    const room = await db.addRoom({ name: 'Leaving' });
    const request = await db.allocate(room, ann);

    await asAdmin(browser, async ({ page }) => {
      await page.goto(url('RoomList.aspx'));
      // Another administrator vacates it while this page is open.
      await db.sql("UPDATE dbo.tbl_RequestRoom SET Status = N'Vacated' WHERE Id = @id; UPDATE dbo.tbl_Room SET Availability = 1 WHERE Id = @room;", { id: request, room });
      await Promise.all([page.waitForNavigation(), page.locator('a[title*="acate"]').click()]);
      assert.equal(await alertText(page), 'This room is no longer allocated.');
      assert.deepEqual((await gridRows(page, 'gvRooms')).map(r => r[3]), ['Available']);
    });
  });
});
