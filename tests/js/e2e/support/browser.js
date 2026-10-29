// Browser sessions for the end-to-end tests.
//
// The master pages load Bootstrap, simple-datatables, Font Awesome and Google Fonts from CDNs. Tests must not depend on
// the internet, so those exact URLs are answered from the pinned npm copies in node_modules (the same versions the
// pages ask for). Any OTHER external request is refused and recorded, so a new CDN dependency shows up in a test.
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const { baseUrl, nodeModules } = require('./config');

const local = (...parts) => path.join(nodeModules, ...parts);

// URL -> what to serve. simple-datatables 7.1.2 ships no .min files on npm (jsDelivr generates them), so the plain
// build is served for them; popper's standalone file is not needed because the Bootstrap bundle contains Popper.
const CDN = new Map([
  ['https://cdn.jsdelivr.net/npm/bootstrap@5.2.3/dist/js/bootstrap.bundle.min.js', { file: local('bootstrap/dist/js/bootstrap.bundle.min.js'), type: 'application/javascript' }],
  ['https://cdn.jsdelivr.net/npm/simple-datatables@7.1.2/dist/umd/simple-datatables.min.js', { file: local('simple-datatables/dist/umd/simple-datatables.js'), type: 'application/javascript' }],
  ['https://cdn.jsdelivr.net/npm/simple-datatables@7.1.2/dist/style.min.css', { file: local('simple-datatables/dist/style.css'), type: 'text/css' }],
  ['https://use.fontawesome.com/releases/v6.3.0/js/all.js', { file: local('@fortawesome/fontawesome-free/js/all.js'), type: 'application/javascript' }],
  ['https://cdn.jsdelivr.net/npm/@popperjs/core@2.10.2/dist/umd/popper.min.js', { body: '', type: 'application/javascript' }],
]);
const GOOGLE_FONTS = /^https:\/\/fonts\.googleapis\.com\//;

async function launch() {
  return chromium.launch({ args: ['--no-sandbox'], executablePath: process.env.CHROMIUM_EXECUTABLE_PATH || undefined });
}

/** A fresh browser profile (no cookies) with its own page. `external` collects every refused outside request. */
async function session(browser) {
  const context = await browser.newContext();
  const external = [];
  await context.route(url => new URL(url).origin !== new URL(baseUrl()).origin, route => {
    const url = route.request().url();
    const hit = CDN.get(url);
    if (hit) return route.fulfill({ status: 200, contentType: hit.type, body: hit.file ? fs.readFileSync(hit.file) : hit.body });
    if (GOOGLE_FONTS.test(url)) return route.fulfill({ status: 200, contentType: 'text/css', body: '' });
    external.push(url);
    return route.abort('blockedbyclient');
  });
  const page = await context.newPage();
  const pageErrors = [];
  page.on('pageerror', e => pageErrors.push(e.message));
  // confirm() prompts (cancel, delete, ...) are accepted, and what they asked is kept so tests can check the wording.
  const dialogs = [];
  page.on('dialog', d => { dialogs.push(d.message()); d.accept(); });
  return { context, page, external, pageErrors, dialogs, close: () => context.close() };
}

const url = relative => new URL(relative, baseUrl()).href;

/** Signs in through the real login page. Resolves once the page has settled; the caller checks where it ended up. */
async function login(page, email, password) {
  await page.goto(url('Login.aspx'));
  await page.fill('[id$="inputEmail"]', email);
  await page.fill('[id$="inputPassword"]', password);
  // Every click on a server button is a postback, which always navigates (to the same URL or a redirect target).
  await Promise.all([page.waitForNavigation(), page.click('[id$="btnLogin"]')]);
}

/** Waits for the shared alert dialog and returns its text (the dialog is left open). */
async function alertText(page) {
  await page.waitForSelector('#modal-alert.show', { timeout: 15000 });
  return (await page.innerText('#alert-message')).trim();
}

/** Closes the alert dialog with its OK button. A message with a redirect navigates afterwards: wait for that yourself. */
async function dismissAlert(page) {
  await page.click('#okButton');
  await page.waitForSelector('#modal-alert.show', { state: 'detached', timeout: 15000 }).catch(() => {});
}

/** Signs out through the account menu, as a user would. */
async function logout(page) {
  await page.click('#navbarDropdown');
  await Promise.all([page.waitForNavigation(), page.click('[id$="lnkLogout"]')]);
}

/** Runs `body(s)` with a fresh session and always closes it. */
async function withSession(browser, body) {
  const s = await session(browser);
  try {
    return await body(s);
  } finally {
    await s.close();
  }
}

/** The path (and query) the browser ended up on, relative to the site root. */
function where(page) {
  const u = new URL(page.url());
  return (u.pathname + u.search).replace(/^\//, '');
}

/** The cell texts of a GridView's body rows: [[cell, cell, ...], ...]. `gridId` is the end of the control's id. */
function gridRows(page, gridId) {
  // A header row that ASP.NET leaves in the body has no td cells and is not data.
  return page.$$eval(`table[id$="${gridId}"] tbody tr`, rows => rows.map(r => [...r.querySelectorAll('td')].map(td => td.innerText.trim())).filter(cells => cells.length > 0));
}

/** The text of the page body with whitespace collapsed. */
async function bodyText(page) {
  return (await page.innerText('body')).replace(/\s+/g, ' ');
}

module.exports = { launch, session, withSession, where, url, login, alertText, dismissAlert, logout, gridRows, bodyText };
