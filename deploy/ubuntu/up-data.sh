#!/usr/bin/env bash
set -euo pipefail

root_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
env_file="$root_dir/deploy/ubuntu/.env"
compose_file="$root_dir/deploy/ubuntu/docker-compose.data.yml"

[[ -f "$env_file" ]] || {
  echo "Create $env_file from deploy/ubuntu/.env.example and set every value." >&2
  exit 1
}

cd "$root_dir"
docker compose --env-file "$env_file" -f "$compose_file" up -d
docker compose --env-file "$env_file" -f "$compose_file" ps
