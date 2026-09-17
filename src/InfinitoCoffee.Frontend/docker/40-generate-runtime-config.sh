#!/bin/sh
set -eu

cat <<EOF >/usr/share/nginx/html/config.js
window.__infinitoCoffeeConfig = Object.freeze({
  apiBaseUrl: '${API_BASE_URL:-http://localhost:5165}',
  signalRHubUrl: '${SIGNALR_HUB_URL:-http://localhost:5165/hubs/orders}',
});
EOF
