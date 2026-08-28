#!/usr/bin/env bash
set -euo pipefail

dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
env_file="$dir/.env"
compose_file="$dir/docker-compose.yml"

[[ -f "$env_file" ]] || {
  echo "Create $env_file from .env.example and set every value." >&2
  exit 1
}

env_get() {
  grep -E "^$1=" "$env_file" | tail -n1 | cut -d= -f2- | tr -d '"' | tr -d "'" || true
}

HCS_DATA_HOST=$(env_get HCS_DATA_HOST)
: "${HCS_DATA_HOST:?set HCS_DATA_HOST in .env}"

HCS_POSTGRES_HOST=$(env_get HCS_POSTGRES_HOST)
HCS_POSTGRES_HOST=${HCS_POSTGRES_HOST:-$HCS_DATA_HOST}
HCS_POSTGRES_WAIT_HOST=$(env_get HCS_POSTGRES_WAIT_HOST)
HCS_POSTGRES_WAIT_HOST=${HCS_POSTGRES_WAIT_HOST:-$HCS_POSTGRES_HOST}

HCS_MINIO_HOST=$(env_get HCS_MINIO_HOST)
HCS_MINIO_HOST=${HCS_MINIO_HOST:-$HCS_DATA_HOST}
HCS_MINIO_WAIT_HOST=$(env_get HCS_MINIO_WAIT_HOST)
HCS_MINIO_WAIT_HOST=${HCS_MINIO_WAIT_HOST:-$HCS_MINIO_HOST}

HCS_REDIS_HOST=$(env_get HCS_REDIS_HOST)
HCS_REDIS_HOST=${HCS_REDIS_HOST:-127.0.0.1}
HCS_REDIS_WAIT_HOST=$(env_get HCS_REDIS_WAIT_HOST)
HCS_REDIS_WAIT_HOST=${HCS_REDIS_WAIT_HOST:-$HCS_REDIS_HOST}

HCS_RABBITMQ_HOST=$(env_get HCS_RABBITMQ_HOST)
HCS_RABBITMQ_HOST=${HCS_RABBITMQ_HOST:-127.0.0.1}
HCS_RABBITMQ_WAIT_HOST=$(env_get HCS_RABBITMQ_WAIT_HOST)
HCS_RABBITMQ_WAIT_HOST=${HCS_RABBITMQ_WAIT_HOST:-$HCS_RABBITMQ_HOST}

wait_port() {
  local host=$1 port=$2 label=${3:-}
  echo "Waiting for ${host}:${port}${label:+ ($label)} ..."
  for _ in $(seq 1 60); do
    if (echo >/dev/tcp/"$host"/"$port") >/dev/null 2>&1; then
      echo "${host}:${port} is reachable"
      return 0
    fi
    sleep 2
  done
  echo "Timed out waiting for ${host}:${port}${label:+ ($label)}" >&2
  echo "Tip: if Postgres/MinIO docker bind 127.0.0.1 only, set HCS_*_WAIT_HOST=127.0.0.1 in .env" >&2
  return 1
}

if [[ "$(env_get HCS_SKIP_PORT_WAIT | tr '[:upper:]' '[:lower:]')" != "true" ]]; then
  wait_port "$HCS_POSTGRES_WAIT_HOST" 5432 "postgres"
  wait_port "$HCS_MINIO_WAIT_HOST" 9000 "minio"
  wait_port "$HCS_REDIS_WAIT_HOST" 6379 "redis"
  wait_port "$HCS_RABBITMQ_WAIT_HOST" 5672 "rabbitmq"
else
  echo "Skipping port wait (HCS_SKIP_PORT_WAIT=true)"
fi

cd "$dir"
if [[ "$(env_get HCS_PULL_IMAGES | tr '[:upper:]' '[:lower:]')" == "true" ]]; then
  echo "Pulling images from Docker Hub (HCS_PULL_IMAGES=true) ..."
  docker compose --env-file "$env_file" -f "$compose_file" pull
fi
docker compose --env-file "$env_file" -f "$compose_file" up -d
docker compose --env-file "$env_file" -f "$compose_file" ps
