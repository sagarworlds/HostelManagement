// The application's own log files (App_Data/Logs of the staged site), for tests that check what was, and was not, logged.
const fs = require('fs');
const path = require('path');

// tests/e2e/run.sh plants this file, dated long ago, before the site starts: it is not something the application wrote.
const PLANTED_OLD_LOG = 'log1-20200101.log';

function logFolder() {
  const site = process.env.E2E_SITE_DIR;
  if (!site) throw new Error('E2E_SITE_DIR is not set (tests/e2e/run.sh sets it).');
  return path.join(site, 'App_Data', 'Logs');
}

/** Every daily log file the application has written, one after the other in date order. */
function readLog() {
  const folder = logFolder();
  return fs.readdirSync(folder)
    .filter(name => /^log1-\d{8}\.log$/.test(name) && name !== PLANTED_OLD_LOG)
    .sort()
    .map(name => fs.readFileSync(path.join(folder, name), 'utf8'))
    .join('\n');
}

module.exports = { PLANTED_OLD_LOG, logFolder, readLog };
