// Settings shared by the end-to-end tests. The run script (tests/e2e/run.sh) sets the environment variables.
const path = require('path');

// The database the staged site was given; never HMSDB. Tests refuse to touch any other name.
const DATABASE = 'HMSDB_E2E';

/** Parses an ADO.NET style connection string ("Server=host,1433;User Id=sa;...") into a lower-cased key/value map. */
function parseConnectionString(text) {
  const map = {};
  for (const part of text.split(';')) {
    const i = part.indexOf('=');
    if (i > 0) map[part.slice(0, i).trim().toLowerCase()] = part.slice(i + 1).trim();
  }
  return map;
}

function sqlServer() {
  const text = process.env.HOSTELHUB_TEST_SQLSERVER;
  if (!text) throw new Error('HOSTELHUB_TEST_SQLSERVER is not set (tests/start-sql-server.sh starts a server and prints it).');
  const map = parseConnectionString(text);
  const [host, port] = (map.server || map['data source'] || 'localhost').replace(/^tcp:/, '').split(',');
  return { host, port: port ? Number(port) : 1433, user: map['user id'] || map.uid, password: map.password || map.pwd };
}

function baseUrl() {
  const url = process.env.E2E_BASE_URL;
  if (!url) throw new Error('E2E_BASE_URL is not set (tests/e2e/run.sh starts the site and sets it).');
  return url.replace(/\/?$/, '/');
}

module.exports = {
  DATABASE,
  parseConnectionString,
  sqlServer,
  baseUrl,
  repoRoot: path.resolve(__dirname, '../../../..'),
  nodeModules: path.resolve(__dirname, '../../node_modules'),
};
