#!/usr/bin/env bash
set -euo pipefail

root_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root_dir"

[[ -f .env ]] || { echo "Create .env from .env.k8s.example and set every value first." >&2; exit 1; }

# Retire the local Kind ingress endpoint without deleting its PVC-backed data.
if command -v kubectl >/dev/null && kubectl get namespace hcs >/dev/null 2>&1; then
  kubectl -n hcs scale deployment --all --replicas=0
  kubectl -n hcs scale statefulset --all --replicas=0 || true
fi

# This proxy occupied the host HTTPS port for the previous Kind ingress.
docker rm -f hcs-ingress-local >/dev/null 2>&1 || true

# docker compose --env-file .env build  blazor
docker compose --env-file .env build
# docker compose --env-file .env up -d postgres redis rabbitmq minio
# docker compose --env-file .env up db-migrator
docker compose --env-file .env up -d auth-server web-gateway blazor platform organization document work-management collaboration bus-management caddy
# docker compose --env-file .env up -d blazor

echo "HCS Compose is running at https://hcs.localhost"
echo "Check status: docker compose ps"
