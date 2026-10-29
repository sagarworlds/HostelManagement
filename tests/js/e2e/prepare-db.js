// Creates the end-to-end database from Database/schema.sql (see support/db.js). Run by tests/e2e/run.sh before the
// site starts; the tests themselves only empty the tables.
require('./support/db').create().then(
  () => console.log('database ready'),
  err => { console.error(err); process.exit(1); });
