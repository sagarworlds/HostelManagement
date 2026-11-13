#!/usr/bin/env bash
# End-to-end tests: the real pages, in a browser, against a real SQL Server.
#
#   HOSTELHUB_TEST_SQLSERVER=... tests/e2e/run.sh [node --test arguments, e.g. a file name]
#
# 1. stage_site.py builds a runnable copy of the site (Mono-only adaptations are listed in that file)
# 2. host.sh serves it with Mono's FastCGI server behind nginx
# 3. the Playwright tests in tests/js/e2e drive it, using the database HMSDB_E2E on the SQL Server
# Needs: dotnet SDK, mono + fastcgi-mono-server4, nginx, node, python3, curl, and a Chromium for Playwright.
set -euo pipefail
cd "$(dirname "$0")/../.."

: "${HOSTELHUB_TEST_SQLSERVER:?set HOSTELHUB_TEST_SQLSERVER (tests/start-sql-server.sh prints it)}"

root="$(pwd)"

# The site signs in as a least-privilege login (Database/create-app-login.sql), with a password invented for this run.
export E2E_APP_LOGIN="hostelhub_e2e"
E2E_APP_PASSWORD="Aa1!$(python3 -c 'import secrets; print(secrets.token_hex(16))')"
export E2E_APP_PASSWORD
# A directory given in E2E_OUT is kept (to inspect the logs); one made here is removed afterwards.
if [ -n "${E2E_OUT:-}" ]; then out="$E2E_OUT"; made_out=0; else out="$(mktemp -d)"; made_out=1; fi

# Absolute paths in cleanup: it runs after this script has changed directory. Stopping is harmless if nothing started.
cleanup() {
  if [ -d "$out/run" ]; then "$root/tests/e2e/host.sh" stop "$out" || true; fi
  if [ "$made_out" = 1 ]; then rm -rf "$out"; fi
}
trap cleanup EXIT

python3 "$root/tests/e2e/stage_site.py" --out "$out" --sql-server "$HOSTELHUB_TEST_SQLSERVER"

# Planted before the site starts, for tests/js/e2e/operations.test.js: an old daily log that log retention must delete, and
# a file that is not one of the logger's own, which it must leave alone.
export E2E_SITE_DIR="$out/site"
mkdir -p "$E2E_SITE_DIR/App_Data/Logs"
echo "an entry from long ago" > "$E2E_SITE_DIR/App_Data/Logs/log1-20200101.log"
echo "not a log file" > "$E2E_SITE_DIR/App_Data/Logs/notes.txt"

# The database is created before the site starts: a running site holds pooled connections that a drop would break.
(cd "$root/tests/js" && npm ci --silent --no-audit --no-fund && node e2e/prepare-db.js)

base_url="$("$root/tests/e2e/host.sh" start "$out")"
echo "site is up at $base_url"

cd tests/js
# One test file at a time: they share one database.
E2E_BASE_URL="$base_url" node --test --test-concurrency=1 "${@:-e2e/*.test.js}"
