// Browser test for HostelManagement/js/grid-search.js (search, sorting and paging of the list grids).
// Runs the real script and the pinned simple-datatables build in headless Chromium against static markup shaped
// like what an ASP.NET GridView renders, including LinkButtons (javascript:__doPostBack + inline confirm).
//
// Usage: node grid-search.test.js        (CHROMIUM_EXECUTABLE_PATH overrides the browser Playwright would pick)
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const pkgDir = path.dirname(require.resolve('simple-datatables/package.json'));
const glueJs = path.resolve(__dirname, '../../HostelManagement/js/grid-search.js');
const umd = path.join(pkgDir, 'dist/umd/simple-datatables.js');
const css = path.join(pkgDir, 'dist/style.css');
const outDir = path.join(__dirname, '.out');
fs.mkdirSync(outDir, { recursive: true });

// Markup shaped like an ASP.NET GridView with a TemplateField action column (LinkButton + OnClientClick).
function gridRows(n) {
  let rows = '';
  for (let i = 1; i <= n; i++) {
    const id = String(i).padStart(2, '0');
    rows += `<tr><td><span id="r${i}">Room ${id}</span></td><td><span>${1000 + i * 10}</span></td><td><span>Available</span></td>` +
      `<td><a onclick="return confirm('Vacate this room? #${i}');" title="Vacate room" ` +
      `href="javascript:__doPostBack(&#39;ctl00$gvRooms&#39;,&#39;Vacate$${i}&#39;)"><span class="material-symbols-outlined">logout</span></a></td></tr>`;
  }
  return rows;
}
const head = '<tr style="font-weight:bold;"><th scope="col">Room No</th><th scope="col">Rent</th><th scope="col">Status</th><th scope="col">Action</th></tr>';
function page({ withLibrary, body }) {
  return `<!doctype html><html><head><meta charset="utf-8"><link rel="stylesheet" href="file://${css}"></head><body>
<script>window.__calls=[];window.__confirms=[];window.__doPostBack=function(t,a){window.__calls.push([t,a]);};window.confirm=function(m){window.__confirms.push(m);return true;};</script>
${body}
${withLibrary ? `<script src="file://${umd}"></script>` : ''}<script src="file://${glueJs}"></script></body></html>`;
}
const withThead = `<table id="t1" class="table table-bordered js-datatable"><thead>${head}</thead><tbody>${gridRows(25)}</tbody></table>`;
const noThead = `<table id="t2" class="table table-bordered js-datatable"><tbody>${head.replace(/th/g,'td')}${gridRows(3)}</tbody></table>`;
const emptyMsg = `<table id="t3" class="table table-bordered js-datatable"><tr><td colspan="4">No rooms have been added yet.</td></tr></table>`;
fs.writeFileSync(path.join(outDir, 'full.html'), page({ withLibrary: true, body: withThead + noThead + emptyMsg }));
fs.writeFileSync(path.join(outDir, 'nolib.html'), page({ withLibrary: false, body: withThead }));

let failures = 0;
const check = (name, ok, detail) => { console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail ? '  [' + detail + ']' : '')); if (!ok) failures++; };

(async () => {
  const browser = await chromium.launch({ args: ['--no-sandbox'], executablePath: process.env.CHROMIUM_EXECUTABLE_PATH || undefined });
  const page1 = await browser.newPage();
  const errors = [];
  page1.on('pageerror', e => errors.push(String(e)));
  await page1.goto('file://' + path.join(outDir, 'full.html'));

  const wrapped = id => page1.evaluate(i => !!document.getElementById(i).closest('.datatable-wrapper'), id);
  check('thead grid is enhanced (wrapper present)', await wrapped('t1'));
  check('search box present', await page1.locator('#t1').locator('xpath=ancestor::*[contains(@class,"datatable-wrapper")]').locator('.datatable-input').count() === 1);
  const visibleRows = () => page1.locator('#t1 tbody tr').count();
  check('first page shows 10 of 25 rows', await visibleRows() === 10, 'rows=' + await visibleRows());
  check('pagination rendered', await page1.locator('.datatable-pagination').count() >= 1);
  check('search box has an accessible name', await page1.locator('.datatable-input').getAttribute('aria-label') === 'Search this list');

  await page1.locator('.datatable-input').pressSequentially('Room 07');
  await page1.waitForFunction(() => document.querySelectorAll('#t1 tbody tr').length === 1, null, { timeout: 3000 }).catch(() => {});
  check('search narrows to the matching row', await visibleRows() === 1 && (await page1.locator('#t1 tbody').innerText()).includes('Room 07'), 'rows=' + await visibleRows());

  // The action link must still post back and still ask for confirmation after the library re-rendered the rows.
  await page1.locator('#t1 tbody a').first().click();
  await page1.waitForFunction(() => window.__calls.length > 0, null, { timeout: 3000 }).catch(() => {});
  const calls = await page1.evaluate(() => ({ calls: window.__calls, confirms: window.__confirms }));
  check('inline onclick confirm survives re-render', calls.confirms.length === 1 && /#7/.test(calls.confirms[0]), JSON.stringify(calls.confirms));
  check('javascript:__doPostBack href survives re-render', calls.calls.length === 1 && calls.calls[0][1] === 'Vacate$7', JSON.stringify(calls.calls));
  check('icon inside the link survives', await page1.locator('#t1 tbody a .material-symbols-outlined').count() === 1);

  await page1.locator('.datatable-input').press('Control+a'); await page1.locator('.datatable-input').press('Backspace');
  await page1.waitForFunction(() => document.querySelectorAll('#t1 tbody tr').length === 10, null, { timeout: 3000 }).catch(() => {});
  check('clearing the search restores the first page', await visibleRows() === 10, 'rows=' + await visibleRows());
  // Every row says "Available" and no other row contains "13": "all words" gives 1 row, "any word" would give all 25.
  await page1.locator('.datatable-input').pressSequentially('available 13');
  await page1.waitForFunction(() => document.querySelectorAll('#t1 tbody tr').length === 1, null, { timeout: 3000 }).catch(() => {});
  const multi = await page1.locator('#t1 tbody tr td:first-child').allInnerTexts();
  check('every typed word must match ("available 13" -> only Room 13)', multi.length === 1 && multi[0].trim() === 'Room 13', JSON.stringify(multi));
  await page1.locator('.datatable-input').press('Control+a'); await page1.locator('.datatable-input').press('Backspace');
  await page1.waitForFunction(() => document.querySelectorAll('#t1 tbody tr').length === 10, null, { timeout: 3000 }).catch(() => {});
  const thTexts = await page1.locator('#t1 thead th').allInnerTexts();
  const actionIdx = thTexts.findIndex(t => t.trim() === 'Action');
  check('Action column is not sortable', await page1.locator('#t1 thead th').nth(actionIdx).locator('.datatable-sorter').count() === 0, 'headers=' + JSON.stringify(thTexts));
  check('other columns are sortable', await page1.locator('#t1 thead th').nth(1).locator('.datatable-sorter').count() === 1);
  await page1.locator('#t1 thead th').nth(1).locator('.datatable-sorter').click();
  await page1.locator('#t1 thead th').nth(1).locator('.datatable-sorter').click();
  const firstRent = (await page1.locator('#t1 tbody tr').first().locator('td').nth(1).innerText()).trim();
  check('sorting by Rent works (descending after two clicks)', firstRent === '1250', 'first=' + firstRent);

  check('grid without <thead> is left alone', !(await wrapped('t2')));
  check('empty-data grid is left alone', !(await wrapped('t3')));
  check('no page errors with the library', errors.length === 0, errors.join(' | '));

  // Why RenderHeaderInThead matters: the library itself on a header-less table.
  const raw = await page1.evaluate(() => { try { const t = document.createElement('table'); t.innerHTML = document.getElementById('t2').innerHTML; document.body.appendChild(t); new simpleDatatables.DataTable(t); return 'initialised, thead cells=' + t.querySelectorAll('thead th').length + ', body rows=' + t.querySelectorAll('tbody tr').length; } catch (e) { return 'throws: ' + e.message; } });
  console.log('INFO  library applied to a header-less table: ' + raw);

  // Library failed to load (e.g. CDN blocked): page must stay usable and silent.
  const page2 = await browser.newPage();
  const errors2 = [];
  page2.on('pageerror', e => errors2.push(String(e)));
  await page2.goto('file://' + path.join(outDir, 'nolib.html'));
  check('without the library: no errors', errors2.length === 0, errors2.join(' | '));
  check('without the library: plain table still has all 25 rows', await page2.locator('#t1 tbody tr').count() === 25);

  await browser.close();
  console.log(failures === 0 ? '\nALL CHECKS PASSED' : '\n' + failures + ' CHECK(S) FAILED');
  process.exit(failures === 0 ? 0 : 1);
})().catch(e => { console.error('HARNESS ERROR', e); process.exit(2); });
