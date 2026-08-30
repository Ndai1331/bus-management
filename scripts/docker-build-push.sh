#!/usr/bin/env bash
# Build and push all HCS images to Docker Hub (local/CI helper).
# Usage:
#   export DOCKERHUB_USERNAME=longnguyen1331
#   export DOCKERHUB_TOKEN=...
#   ./scripts/docker-build-push.sh
set -euo pipefail

root_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root_dir"

registry=${HCS_DOCKER_REGISTRY:-longnguyen1331/hanhchinhso}

if [[ -z "${DOCKERHUB_USERNAME:-}" || -z "${DOCKERHUB_TOKEN:-}" ]]; then
  echo "Set DOCKERHUB_USERNAME and DOCKERHUB_TOKEN before pushing." >&2
  exit 1
fi

echo "$DOCKERHUB_TOKEN" | docker login -u "$DOCKERHUB_USERNAME" --password-stdin

build_one() {
  local service=$1 project=$2 app_dll=$3
  local publish_properties=${4:-}
  local install_libreoffice=${5:-false}

  echo "==> Building ${registry}:${service}"
  docker build \
    -f deploy/docker/Dockerfile \
    --build-arg "PROJECT=${project}" \
    --build-arg "APP_DLL=${app_dll}" \
    --build-arg "PUBLISH_PROPERTIES=${publish_properties}" \
    --build-arg "INSTALL_LIBREOFFICE=${install_libreoffice}" \
    -t "${registry}:${service}" \
    .
  docker push "${registry}:${service}"
}

if [[ "${SKIP_ABP_LIBS:-false}" != "true" ]]; then
  echo "==> abp install-libs (auth-server)"
  (cd apps/auth-server/HCS.AuthServer && env YARN_IGNORE_ENGINES=1 abp install-libs)
fi

build_one db-migrator src/HCS.DbMigrator/HCS.DbMigrator.csproj HCS.DbMigrator.dll
build_one auth-server apps/auth-server/HCS.AuthServer/HCS.AuthServer.csproj HCS.AuthServer.dll
build_one web-gateway gateways/web/HCS.WebGateway/HCS.WebGateway.csproj HCS.WebGateway.dll
build_one blazor src/HCS.Blazor/HCS.Blazor.csproj HCS.Blazor.dll '/p:BlazorEnableCompression=false'
build_one platform services/platform/HCS.PlatformService/HCS.PlatformService.csproj HCS.PlatformService.dll
build_one organization services/organization/HCS.OrganizationService.Host/HCS.OrganizationService.Host.csproj HCS.OrganizationService.Host.dll
build_one document services/document/HCS.DocumentService/HCS.DocumentService.csproj HCS.DocumentService.dll '' true
build_one work-management services/work-management/HCS.WorkManagementService/HCS.WorkManagementService.csproj HCS.WorkManagementService.dll
build_one collaboration services/collaboration/HCS.CollaborationService/HCS.CollaborationService.csproj HCS.CollaborationService.dll
build_one bus-management services/bus-management/HCS.BusManagementService/HCS.BusManagementService.csproj HCS.BusManagementService.dll

echo "Done. Images pushed to ${registry}:<service>"
