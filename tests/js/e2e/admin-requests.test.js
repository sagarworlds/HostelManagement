// The administrator's request queue: dashboard statistics, filtering, deciding requests, and two administrators at once.
const { describe, it, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const { launch, withSession, where, url, login, alertText, gridRows, bodyText } = require('./support/browser');
const db = require('./support/db');

const asAdmin = (browser, body) => withSession(browser, async s => { await login(s.page, db.ADMIN_EMAIL, db.PASSWORD); return body(s); });

describe('administrator: requests', () => {
  let browser;
  before(async () => { await db.clear(); browser = await launch(); });
  after(async () => { await browser.close(); });
  beforeEach(() => db.clear());

  describe('dashboard', () => {
    it('shows the headline numbers for the whole hostel', async () => {
      const ann = await db.addUser({ email: 'ann@example.test' });
      const bob = await db.addUser({ email: 'bob@example.test' });
      await db.addUser({ email: 'cy@example.test' });
      const held = await db.addRoom({ name: 'Held' });
      await db.addRoom({ name: 'Free 1' });
      await db.addRoom({ name: 'Free 2' });
      await db.allocate(held, ann);
      await db.addRequest({ userId: bob });
      await db.addUser({ type: 'Admin', email: 'second@example.test' });

      await asAdmin(browser, async ({ page }) => {
        assert.equal(where(page), 'adminDashboard.aspx');
        const text = await bodyText(page);
        const tile = label => Number(new RegExp('(\\d+) (?:View details )?' + label).exec(text)?.[1]);
        assert.equal(tile('Pending requests'), 1);
        assert.equal(tile('Total rooms'), 3);
        assert.equal(tile('Available rooms'), 2);
        assert.equal(tile('Occupied rooms'), 1);
        assert.equal(tile('Students'), 3);
        assert.equal(tile('Admins'), 2);
      });
    });

    it('lists requests with the student, preferred room and allocated room, and filters them by status', async () => {
      const ann = await db.addUser({ email: 'ann@example.test', first: 'Ann', last: '<i>Italic</i>' });
      const bob = await db.addUser({ email: 'bob@example.test', first: 'Bob', last: 'Student' });
      const cy = await db.addUser({ email: 'cy@example.test', first: 'Cy', last: 'Student' });
      const wanted = await db.addRoom({ name: 'Sea view' });
      const held = await db.addRoom({ name: 'Held' });
      await db.addRequest({ userId: ann, preferredRoomId: wanted });
      await db.allocate(held, bob);
      await db.addRequest({ userId: cy, status: 'Rejected' });

      await asAdmin(browser, async ({ page }) => {
        const all = await gridRows(page, 'gvRoomsRequest');
        // Pending first, then newest first; the student's markup is shown as text.
        assert.deepEqual(all.map(r => [r[1], r[3], r[4], r[5]]), [
          ['Ann <i>Italic</i>', 'Pending', 'Sea view', ''],
          ['Cy Student', 'Rejected', '', ''],
          ['Bob Student', 'Approved', '', 'Held'],
        ]);

        for (const [status, expected] of [['Pending', ['Pending']], ['Rejected', ['Rejected']], ['Approved', ['Approved']]]) {
          await Promise.all([page.waitForNavigation(), page.selectOption('select[id$="ddlStatusFilter"]', { label: status })]);
          assert.deepEqual((await gridRows(page, 'gvRoomsRequest')).map(r => r[3]), expected, `filter ${status}`);
        }
        await Promise.all([page.waitForNavigation(), page.selectOption('select[id$="ddlStatusFilter"]', { label: 'Vacated' })]);
        assert.match(await bodyText(page), /There are no room requests\./);
      });
    });
  });

  describe('deciding a request', () => {
    async function openRequest(page, requestId) {
      await page.goto(url('ApproveRoomRequest.aspx?Id=' + requestId));
    }

    it('approves a request into the room the student asked for, and the student sees it', async () => {
      const ann = await db.addUser({ email: 'ann@example.test', first: 'Ann', last: 'Student' });
      const wanted = await db.addRoom({ name: 'Sea view' });
      await db.addRoom({ name: 'Other' });
      const request = await db.addRequest({ userId: ann, preferredRoomId: wanted });

      await asAdmin(browser, async ({ page }) => {
        await openRequest(page, request);
        const text = await bodyText(page);
        assert.match(text, /Ann Student/);
        assert.match(text, /Pending/);
        assert.match(text, /Sea view/);
        assert.equal(await page.locator('select[id$="ddlAvailableRooms"] option:checked').innerText(), 'Sea view', 'the preferred room is already selected');

        await Promise.all([page.waitForNavigation(), page.click('[id$="btnApprove"]')]);
        assert.equal(where(page), 'adminDashboard.aspx');
        assert.equal(await alertText(page), 'Request approved.');
        const [row] = await gridRows(page, 'gvRoomsRequest');
        assert.deepEqual([row[3], row[5]], ['Approved', 'Sea view']);
      });

      assert.equal((await db.sql('SELECT Availability FROM dbo.tbl_Room WHERE Id = @id', { id: wanted }))[0].Availability, false);
      await withSession(browser, async ({ page }) => {
        await login(page, 'ann@example.test', db.PASSWORD);
        const [row] = await gridRows(page, 'gvRoomsRequest');
        assert.deepEqual([row[2], row[4]], ['Approved', 'Sea view']);
      });
    });

    it('lets the administrator choose a different room, and points out when the preferred one has gone', async () => {
      const ann = await db.addUser({ email: 'ann@example.test' });
      const bob = await db.addUser({ email: 'bob@example.test' });
      const wanted = await db.addRoom({ name: 'Sea view' });
      const other = await db.addRoom({ name: 'Other' });
      await db.allocate(wanted, bob);
      const request = await db.addRequest({ userId: ann, preferredRoomId: wanted });

      await asAdmin(browser, async ({ page }) => {
        await openRequest(page, request);
        assert.match(await bodyText(page), /Sea view \(not available\)/);
        assert.equal(await page.locator('select[id$="ddlAvailableRooms"] option:checked').innerText(), 'Select Room');

        await page.click('[id$="btnApprove"]');                 // nothing chosen: refused before anything is sent
        await page.waitForSelector('text=Select a room.');
        assert.equal((await db.sql('SELECT Status FROM dbo.tbl_RequestRoom WHERE Id = @id', { id: request }))[0].Status, 'Pending');

        await page.selectOption('select[id$="ddlAvailableRooms"]', { label: 'Other' });
        await Promise.all([page.waitForNavigation(), page.click('[id$="btnApprove"]')]);
        assert.equal(await alertText(page), 'Request approved.');
      });
      const [row] = await db.sql('SELECT ApprovedRoomId FROM dbo.tbl_RequestRoom WHERE Id = @id', { id: request });
      assert.equal(row.ApprovedRoomId, other);
    });

    it('can only reject when no room is free', async () => {
      const ann = await db.addUser({ email: 'ann@example.test' });
      const request = await db.addRequest({ userId: ann });

      await asAdmin(browser, async ({ page }) => {
        await openRequest(page, request);
        assert.equal(await page.locator('select[id$="ddlAvailableRooms"] option:checked').innerText(), 'No rooms available');
        assert.equal(await page.isDisabled('[id$="btnApprove"]'), true);
        await Promise.all([page.waitForNavigation(), page.click('[id$="btnReject"]')]);
        assert.equal(await alertText(page), 'Request rejected.');
        assert.deepEqual((await gridRows(page, 'gvRoomsRequest')).map(r => r[3]), ['Rejected']);
      });
    });

    it('sends the administrator back to the dashboard with a reason for a stale or invented link', async () => {
      const ann = await db.addUser({ email: 'ann@example.test' });
      const done = await db.addRequest({ userId: ann, status: 'Rejected' });

      await asAdmin(browser, async ({ page }) => {
        await openRequest(page, done);
        assert.equal(where(page), 'adminDashboard.aspx');
        assert.equal(await alertText(page), 'That request has already been processed.');
        await openRequest(page, 987654);
        assert.equal(await alertText(page), 'That request no longer exists.');
        await page.goto(url('ApproveRoomRequest.aspx?Id=abc'));
        assert.equal(await alertText(page), 'That request no longer exists.');
      });
    });
  });

  describe('two administrators at once', () => {
    it('the second to decide the same request is told it has already been processed', async () => {
      const ann = await db.addUser({ email: 'ann@example.test' });
      await db.addRoom({ name: 'Room A' });
      const request = await db.addRequest({ userId: ann });
      await db.addUser({ type: 'Admin', email: 'second@example.test', first: 'Second', last: 'Admin' });

      await asAdmin(browser, async first => {
        await first.page.goto(url('ApproveRoomRequest.aspx?Id=' + request));
        await first.page.selectOption('select[id$="ddlAvailableRooms"]', { label: 'Room A' });

        await withSession(browser, async second => {
          await login(second.page, 'second@example.test', db.PASSWORD);
          await second.page.goto(url('ApproveRoomRequest.aspx?Id=' + request));
          await Promise.all([second.page.waitForNavigation(), second.page.click('[id$="btnReject"]')]);
          assert.equal(await alertText(second.page), 'Request rejected.');
        });

        await Promise.all([first.page.waitForNavigation(), first.page.click('[id$="btnApprove"]')]);
        assert.equal(await alertText(first.page), 'This request has already been processed.');
      });
      assert.equal((await db.sql('SELECT Status FROM dbo.tbl_RequestRoom WHERE Id = @id', { id: request }))[0].Status, 'Rejected');
      assert.equal((await db.sql('SELECT Availability FROM dbo.tbl_Room'))[0].Availability, true, 'the room was not taken');
    });

    it('the second to take the same room is told to choose another, and the list is refreshed', async () => {
      const ann = await db.addUser({ email: 'ann@example.test' });
      const bob = await db.addUser({ email: 'bob@example.test' });
      await db.addRoom({ name: 'Only room' });
      await db.addRoom({ name: 'Spare room' });
      const r1 = await db.addRequest({ userId: ann });
      const r2 = await db.addRequest({ userId: bob });
      await db.addUser({ type: 'Admin', email: 'second@example.test' });

      await asAdmin(browser, async first => {
        await first.page.goto(url('ApproveRoomRequest.aspx?Id=' + r2));
        await first.page.selectOption('select[id$="ddlAvailableRooms"]', { label: 'Only room' });

        await withSession(browser, async second => {
          await login(second.page, 'second@example.test', db.PASSWORD);
          await second.page.goto(url('ApproveRoomRequest.aspx?Id=' + r1));
          await second.page.selectOption('select[id$="ddlAvailableRooms"]', { label: 'Only room' });
          await Promise.all([second.page.waitForNavigation(), second.page.click('[id$="btnApprove"]')]);
          assert.equal(await alertText(second.page), 'Request approved.');
        });

        await Promise.all([first.page.waitForNavigation(), first.page.click('[id$="btnApprove"]')]);
        assert.equal(await alertText(first.page), 'The selected room is no longer available. Choose another room.');
        const options = await first.page.locator('select[id$="ddlAvailableRooms"] option').allInnerTexts();
        assert.deepEqual(options, ['Select Room', 'Spare room'], 'only rooms that are still free are offered');
        assert.equal((await db.sql('SELECT Status FROM dbo.tbl_RequestRoom WHERE Id = @id', { id: r2 }))[0].Status, 'Pending');
      });
    });
  });
});
