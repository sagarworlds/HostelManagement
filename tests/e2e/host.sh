#!/usr/bin/env bash
# Starts or stops the staged site (see stage_site.py): Mono's FastCGI server runs the ASP.NET application and nginx
# serves it on http://127.0.0.1:$E2E_PORT (default 18080).
#
#   tests/e2e/host.sh start DIR     DIR is the --out directory given to stage_site.py; prints the site's URL
#   tests/e2e/host.sh stop DIR
#
# Needs: mono, fastcgi-mono-server4 (Debian/Ubuntu: mono-complete mono-fastcgi-server4) and nginx.
# Processes are tracked by the PIDs written under DIR/run, never by name, so stopping cannot hit anything else.
set -euo pipefail

action="${1:-}"
dir="${2:-}"
[ -n "$action" ] && [ -n "$dir" ] || { echo "usage: $0 start|stop DIR" >&2; exit 2; }
dir="$(cd "$dir" && pwd)"
run="$dir/run"
site="$dir/site"
port="${E2E_PORT:-18080}"
fcgi_port="${E2E_FCGI_PORT:-19000}"

# True when something already accepts connections on the port (bash's /dev/tcp needs no extra tools).
port_in_use() {
  (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null
}

stop_pid() {
  local file="$1"
  if [ -f "$file" ]; then
    kill "$(cat "$file")" 2>/dev/null || true
    rm -f "$file"
  fi
}

case "$action" in
  start)
    mkdir -p "$run/tmp"
    # A leftover from an earlier run in this same directory is stopped first; anything else on the ports is not ours.
    stop_pid "$run/fcgi.pid"
    stop_pid "$run/nginx.pid"
    sleep 1
    for p in "$port" "$fcgi_port"; do
      if port_in_use "$p"; then
        echo "port $p is already in use by something else; set E2E_PORT / E2E_FCGI_PORT to free ports" >&2
        exit 1
      fi
    done
    cat > "$run/nginx.conf" <<EOF
daemon off;
pid $run/nginx-master.pid;
error_log $run/nginx-error.log;
events { worker_connections 64; }
http {
  access_log $run/nginx-access.log;
  client_body_temp_path $run/tmp/body; proxy_temp_path $run/tmp/proxy; fastcgi_temp_path $run/tmp/fcgi;
  uwsgi_temp_path $run/tmp/uwsgi; scgi_temp_path $run/tmp/scgi;
  server {
    listen 127.0.0.1:$port;
    server_name localhost;
    root $site;
    location / {
      index Login.aspx;
      fastcgi_index Login.aspx;
      fastcgi_pass 127.0.0.1:$fcgi_port;
      include /etc/nginx/fastcgi_params;
      fastcgi_param PATH_INFO "";
      fastcgi_param SCRIPT_FILENAME \$document_root\$fastcgi_script_name;
    }
  }
}
EOF
    # Each server is started directly (no subshell, no setsid), so $! is the server's own PID, and all three standard
    # streams are detached: a server that kept this script's stdout open would stop `$(host.sh start ...)` returning.
    cd "$site"
    nohup fastcgi-mono-server4 "/applications=/:$site" "/socket=tcp:127.0.0.1:$fcgi_port" \
        "/logfile=$run/fcgi.log" /loglevels=Standard > "$run/fcgi.out" 2>&1 < /dev/null &
    echo $! > "$run/fcgi.pid"
    nohup nginx -c "$run/nginx.conf" > "$run/nginx.out" 2>&1 < /dev/null &
    echo $! > "$run/nginx.pid"
    cd - > /dev/null

    for _ in $(seq 1 60); do
      if [ "$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$port/Login.aspx" || true)" = "200" ]; then
        echo "http://127.0.0.1:$port/"
        exit 0
      fi
      sleep 1
    done
    echo "the site did not answer on port $port; see $run/fcgi.out, $run/fcgi.log and $run/nginx-error.log" >&2
    "$0" stop "$dir" || true
    exit 1
    ;;
  stop)
    stop_pid "$run/fcgi.pid"
    stop_pid "$run/nginx.pid"
    ;;
  *)
    echo "usage: $0 start|stop DIR" >&2
    exit 2
    ;;
esac
