#!/usr/bin/env bash
# Runs every check that works on Linux/macOS. This is what the "CI (Linux)" workflow runs, so a green local run
# means a green GitHub run.
#
#   tests/run-local.sh                 run everything; a missing tool is reported as SKIPPED
#   tests/run-local.sh --require-all   a missing tool is a failure (used by CI)
#
# The Windows-only half (MSBuild of the real project + ASPX precompile) is scripts/build-windows.ps1.
set -u
cd "$(dirname "$0")/.."

require_all=0
[ "${1:-}" = "--require-all" ] && require_all=1

failed=0
skipped=0
summary=()

run() {
  local name="$1"; shift
  echo; echo "=== $name ==="
  if "$@"; then summary+=("PASS     $name"); else summary+=("FAIL     $name"); failed=1; fi
}

skip_or_fail() {
  local name="$1" reason="$2"
  echo; echo "=== $name ==="; echo "$reason"
  if [ "$require_all" = 1 ]; then summary+=("FAIL     $name ($reason)"); failed=1
  else summary+=("SKIPPED  $name ($reason)"); skipped=1; fi
}

# 1. Static checks: need only python3.
if command -v python3 >/dev/null 2>&1; then
  for check in check_csproj check_designers check_sql_parameters check_markup; do
    run "static: $check" python3 "tests/static/$check.py"
  done
else
  skip_or_fail "static checks" "python3 not found"
fi

# 2. C# compile check and unit tests: need the .NET SDK (8.0 or newer).
if command -v dotnet >/dev/null 2>&1; then
  run "C# compiles against .NET Framework 4.8" dotnet build tests/dotnet/CompileCheck/CompileCheck.csproj -nologo -v quiet -o "${TMPDIR:-/tmp}/hostelhub-compilecheck"
  run "unit tests" dotnet test tests/dotnet/UnitTests/UnitTests.csproj -nologo -v quiet
else
  skip_or_fail "C# compile check and unit tests" "dotnet SDK not found"
fi

# 3. Browser test of js/grid-search.js: needs node and a Chromium that Playwright can find.
if command -v node >/dev/null 2>&1 && command -v npm >/dev/null 2>&1; then
  run "browser: grid search" bash -c 'cd tests/js && npm ci --silent --no-audit --no-fund && node grid-search.test.js'
else
  skip_or_fail "browser: grid search" "node/npm not found"
fi

echo; echo "================ summary ================"
printf '%s\n' "${summary[@]}"
if [ "$failed" = 1 ]; then echo "RESULT: FAILED"; exit 1; fi
if [ "$skipped" = 1 ]; then echo "RESULT: passed, but some checks were skipped"; exit 0; fi
echo "RESULT: all checks passed"
