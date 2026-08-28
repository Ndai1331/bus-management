#!/usr/bin/env bash
set -euo pipefail

dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
env_file="$dir/.env"
compose_file="$dir/docker-compose.yml"

[[ -f "$env_file" ]] || {
  echo "Create $env_file from .env.example and set every value." >&2
  exit 1
}

cd "$dir"
docker compose --env-file "$env_file" -f "$compose_file" up -d
docker compose --env-file "$env_file" -f "$compose_file" ps
