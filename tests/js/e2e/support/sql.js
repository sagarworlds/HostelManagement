// Minimal SQL Server access for arranging and checking data (tedious, pure JavaScript).
const { Connection, Request, TYPES } = require('tedious');
const { sqlServer } = require('./config');

function connect(database) {
  const { host, port, user, password } = sqlServer();
  return new Promise((resolve, reject) => {
    const connection = new Connection({
      server: host,
      authentication: { type: 'default', options: { userName: user, password } },
      options: { port, database, encrypt: false, trustServerCertificate: true, rowCollectionOnRequestCompletion: true, requestTimeout: 120000 },
    });
    connection.on('connect', err => (err ? reject(err) : resolve(connection)));
    connection.connect();
  });
}

/** Runs one batch on an open connection and returns its rows as plain objects. `params` is {name: value}, used as @name. */
function execute(connection, text, params = {}) {
  return new Promise((resolve, reject) => {
    const request = new Request(text, (err, rowCount, rows) => {
      if (err) return reject(err);
      resolve((rows || []).map(columns => Object.fromEntries(columns.map(c => [c.metadata.colName, c.value]))));
    });
    for (const [name, value] of Object.entries(params)) {
      // Strings are sent as NVARCHAR so Unicode survives; numbers as INT; booleans as BIT. A null is a typed NULL, which
      // SQL Server converts to whatever column it is stored in. Times are computed in SQL (GETDATE/DATEADD), not passed.
      const type = typeof value === 'number' ? TYPES.Int : typeof value === 'boolean' ? TYPES.Bit : TYPES.NVarChar;
      request.addParameter(name, type, value);
    }
    connection.execSql(request);
  });
}

/** One batch on its own connection. */
async function query(database, text, params = {}) {
  const connection = await connect(database);
  try {
    return await execute(connection, text, params);
  } finally {
    connection.close();
  }
}

/** Splits a script into batches at lines that hold only GO, as SSMS and sqlcmd do. */
function batches(script) {
  return script.split(/^[ \t]*GO[ \t]*\r?$/im).map(b => b.trim()).filter(b => b.length > 0);
}

/** Runs a whole script batch by batch on ONE connection (so USE and other session state carry over); stops at the first error. */
async function runScript(database, script) {
  const connection = await connect(database);
  try {
    for (const batch of batches(script)) await execute(connection, batch);
  } finally {
    connection.close();
  }
}

module.exports = { query, runScript, batches };
