#!/usr/bin/env bash
# Starts a throw-away SQL Server in Docker for the database tests (tests/dotnet/IntegrationTests) and prints the line
# that points the tests at it.
#
#   eval "$(tests/start-sql-server.sh)"      start (or reuse) the server and set HOSTELHUB_TEST_SQLSERVER
#   tests/start-sql-server.sh --stop         remove the server
#
# The server only listens on this machine's loopback address and holds nothing but test databases. Extra arguments
# for "docker run" can be given in HOSTELHUB_TEST_DOCKER_ARGS (for example "--network host" where port publishing is
# unavailable); they replace the default port mapping.
set -euo pipefail

name=hostelhub-test-sql
# Pinned so a local run and CI use the same server version.
image=mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04
# Test-only value for a container nobody else can reach; SQL Server insists on a "complex" password.
password='Hm!Test-Only-9xQ2vL7'
port="${HOSTELHUB_TEST_SQL_PORT:-14330}"

if [ "${1:-}" = "--stop" ]; then
  docker rm -f "$name" >/dev/null 2>&1 || true
  echo "stopped $name" >&2
  exit 0
fi

if ! docker ps --format '{{.Names}}' | grep -qx "$name"; then
  docker rm -f "$name" >/dev/null 2>&1 || true
  # shellcheck disable=SC2086  # the extra arguments are meant to be split
  docker run -d --name "$name" ${HOSTELHUB_TEST_DOCKER_ARGS:--p 127.0.0.1:$port:1433} \
    -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$password" -e MSSQL_PID=Developer "$image" >/dev/null
fi

# With "--network host" the server listens on 1433 whatever HOSTELHUB_TEST_SQL_PORT says (also when reusing a container).
case "${HOSTELHUB_TEST_DOCKER_ARGS:-}" in *"--network host"*) port=1433;; esac

echo "waiting for SQL Server to accept connections..." >&2
for _ in $(seq 1 60); do
  if docker exec "$name" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$password" -Q 'SELECT 1' >/dev/null 2>&1; then
    echo "export HOSTELHUB_TEST_SQLSERVER='Server=localhost,$port;User Id=sa;Password=$password;TrustServerCertificate=True;Encrypt=False'"
    exit 0
  fi
  sleep 2
done

echo "SQL Server did not become ready in time; see: docker logs $name" >&2
exit 1
