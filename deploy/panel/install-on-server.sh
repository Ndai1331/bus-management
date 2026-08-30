#!/usr/bin/env bash
# Sync HCS panel bundle to Baota compose directory on a remote server.
# Usage: ./deploy/panel/install-on-server.sh [user@host]
set -euo pipefail

remote=${1:?Usage: $0 user@host}
panel_root=/www/server/panel/data/compose

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

echo "Installing to ${remote}:${panel_root}/hcs-infra and .../hcs"

ssh "$remote" "mkdir -p ${panel_root}/hcs-data/postgres ${panel_root}/hcs-infra ${panel_root}/hcs/config ${panel_root}/hcs/.hcs-certs"

rsync -av --delete \
  --exclude '.env' \
  "$script_dir/hcs-data/" "${remote}:${panel_root}/hcs-data/"

rsync -av --delete \
  --exclude '.env' \
  "$script_dir/hcs-infra/" "${remote}:${panel_root}/hcs-infra/"

rsync -av --delete \
  --exclude '.env' \
  --exclude '.hcs-certs/' \
  "$script_dir/hcs/" "${remote}:${panel_root}/hcs/"

ssh "$remote" "chmod +x ${panel_root}/hcs-data/up.sh ${panel_root}/hcs-infra/up.sh ${panel_root}/hcs/up.sh"

echo ""
echo "Done. On apps server (10.17.227.58):"
echo "  cp ${panel_root}/hcs/.env.example ${panel_root}/hcs/.env"
echo "  # edit .env — redis+rabbit đã gộp trong hcs/docker-compose.yml"
echo "  ${panel_root}/hcs/up.sh"
echo ""
echo "Optional separate infra project:"
echo "  cp ${panel_root}/hcs-infra/.env.example ${panel_root}/hcs-infra/.env"
echo "  ${panel_root}/hcs-infra/up.sh  # then set HCS_REDIS_HOST=host.docker.internal in hcs/.env"
echo ""
echo "See deploy/panel/README.md for Nginx, Keycloak bootstrap, and SSL."
