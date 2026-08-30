#!/usr/bin/env bash
set -euo pipefail

root_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
env_file=${HCS_ENV_FILE:-"$root_dir/.env"}
namespace=${HCS_NAMESPACE:-hcs}
release=${HCS_RELEASE:-hcs}
use_kind=false

if [[ ${1:-} == "--kind" ]]; then use_kind=true; fi
for command in docker kubectl helm; do command -v "$command" >/dev/null || { echo "Missing required command: $command" >&2; exit 1; }; done
[[ -f "$env_file" ]] || { echo "Create $env_file from .env.example first." >&2; exit 1; }
if ! bash -n "$env_file" 2>/dev/null; then
  echo "Invalid .env syntax. Use KEY=value (no <...>, spaces, or unquoted special characters)." >&2
  exit 1
fi

if $use_kind; then
  command -v kind >/dev/null || { echo "--kind requires kind." >&2; exit 1; }
  kind get clusters | grep -qx hcs || kind create cluster --name hcs
fi

set -a
# shellcheck disable=SC1090
source "$env_file"
set +a
for key in HCS_POSTGRES_PASSWORD HCS_MINIO_ROOT_USER HCS_MINIO_ROOT_PASSWORD HCS_RABBITMQ_USER HCS_RABBITMQ_PASSWORD HCS_ADMIN_PASSWORD HCS_GATEWAY_CLIENT_SECRET HCS_PUBLIC_HOST HCS_AUTH_PUBLIC_HOST HCS_TLS_SECRET HCS_OPENIDDICT_PFX HCS_DATAPROTECTION_PFX HCS_OPENIDDICT_PFX_PASSWORD HCS_DATAPROTECTION_PFX_PASSWORD HCS_KEYCLOAK_AUTHORITY HCS_KEYCLOAK_CLIENT_SECRET; do
  [[ -n ${!key:-} && ${!key} != change-me ]] || { echo "Set $key in $env_file." >&2; exit 1; }
done
if $use_kind; then
  # Kind receives local Docker images directly; never use a remote registry example here.
  image_prefix=hcs
  ingress_controller_ip=$(kubectl -n ingress-nginx get service ingress-nginx-controller -o jsonpath='{.spec.clusterIP}' 2>/dev/null || true)
  allow_untrusted_backchannel_certificate=true
else
  if [[ -z ${HCS_IMAGE_REGISTRY:-} || $HCS_IMAGE_REGISTRY == *.example || $HCS_IMAGE_REGISTRY == registry.example ]]; then
    echo "Set HCS_IMAGE_REGISTRY to a real registry DNS name for a remote cluster, or use --kind." >&2
    exit 1
  fi
  image_prefix=${HCS_IMAGE_REGISTRY%/}/hcs
  ingress_controller_ip=""
  allow_untrusted_backchannel_certificate=false
fi

build() {
  local image=$1 project=$2 dll=$3
  docker build --file "$root_dir/deploy/docker/Dockerfile" --tag "$image:dev" \
    --build-arg "PROJECT=$project" --build-arg "APP_DLL=$dll" "$root_dir"
  if $use_kind; then kind load docker-image "$image:dev" --name hcs; fi
  if ! $use_kind; then docker push "$image:dev"; fi
}

if [[ ${HCS_SKIP_BUILD:-false} != true ]]; then
  build "$image_prefix/auth-server" apps/auth-server/HCS.AuthServer/HCS.AuthServer.csproj HCS.AuthServer.dll
  build "$image_prefix/web-gateway" gateways/web/HCS.WebGateway/HCS.WebGateway.csproj HCS.WebGateway.dll
  build "$image_prefix/blazor" src/HCS.Blazor/HCS.Blazor.csproj HCS.Blazor.dll
  build "$image_prefix/platform" services/platform/HCS.PlatformService/HCS.PlatformService.csproj HCS.PlatformService.dll
  build "$image_prefix/organization" services/organization/HCS.OrganizationService.Host/HCS.OrganizationService.Host.csproj HCS.OrganizationService.Host.dll
  build "$image_prefix/document" services/document/HCS.DocumentService/HCS.DocumentService.csproj HCS.DocumentService.dll
  build "$image_prefix/work-management" services/work-management/HCS.WorkManagementService/HCS.WorkManagementService.csproj HCS.WorkManagementService.dll
  build "$image_prefix/collaboration" services/collaboration/HCS.CollaborationService/HCS.CollaborationService.csproj HCS.CollaborationService.dll
  build "$image_prefix/bus-management" services/bus-management/HCS.BusManagementService/HCS.BusManagementService.csproj HCS.BusManagementService.dll
  build "$image_prefix/db-migrator" src/HCS.DbMigrator/HCS.DbMigrator.csproj HCS.DbMigrator.dll
fi

runtime_env=$(mktemp)
trap 'rm -f "$runtime_env"' EXIT
cat >"$runtime_env" <<EOF
HCS_POSTGRES_PASSWORD=$HCS_POSTGRES_PASSWORD
HCS_MINIO_ROOT_USER=$HCS_MINIO_ROOT_USER
HCS_MINIO_ROOT_PASSWORD=$HCS_MINIO_ROOT_PASSWORD
HCS_RABBITMQ_USER=$HCS_RABBITMQ_USER
HCS_RABBITMQ_PASSWORD=$HCS_RABBITMQ_PASSWORD
Identity__AdminPassword=$HCS_ADMIN_PASSWORD
Authentication__Authority=https://$HCS_AUTH_PUBLIC_HOST
Authentication__ClientId=HCS_App
Authentication__ClientSecret=$HCS_GATEWAY_CLIENT_SECRET
Authentication__RequireHttpsMetadata=true
Authentication__AllowUntrustedBackchannelCertificate=$allow_untrusted_backchannel_certificate
Authentication__Keycloak__Enabled=true
Authentication__Keycloak__Authority=$HCS_KEYCLOAK_AUTHORITY
Authentication__Keycloak__ClientId=hcs-free-auth
Authentication__Keycloak__ClientSecret=$HCS_KEYCLOAK_CLIENT_SECRET
Authentication__Keycloak__RequireHttpsMetadata=true
AuthServer__Authority=https://$HCS_AUTH_PUBLIC_HOST
AuthServer__RequireHttpsMetadata=true
App__SelfUrl=https://$HCS_PUBLIC_HOST
App__CorsOrigins__0=https://$HCS_PUBLIC_HOST
Bff__PublicOrigin=https://$HCS_PUBLIC_HOST
Bff__CookieDomain=${HCS_COOKIE_DOMAIN:-}
DataProtection__Redis=redis:6379
Redis__Configuration=redis:6379
RabbitMQ__Connections__Default__HostName=rabbitmq
RabbitMQ__Connections__Default__UserName=$HCS_RABBITMQ_USER
RabbitMQ__Connections__Default__Password=$HCS_RABBITMQ_PASSWORD
Minio__EndPoint=minio:9000
Minio__AccessKey=$HCS_MINIO_ROOT_USER
Minio__SecretKey=$HCS_MINIO_ROOT_PASSWORD
Minio__WithSSL=false
OpenIddict__Applications__HCS_App__ClientSecret=$HCS_GATEWAY_CLIENT_SECRET
OpenIddict__Applications__HCS_App__RootUrl=https://$HCS_PUBLIC_HOST/
OpenIddict__Applications__HCS_App__PostLogoutRootUrl=https://$HCS_PUBLIC_HOST/
AuthServer__CertificatePath=/var/run/hcs-certs/openiddict.pfx
AuthServer__CertificatePassword=$HCS_OPENIDDICT_PFX_PASSWORD
DataProtection__Certificate__Path=/var/run/hcs-certs/dataprotection.pfx
DataProtection__Certificate__Password=$HCS_DATAPROTECTION_PFX_PASSWORD
DataProtection__CertificatePath=/var/run/hcs-certs/dataprotection.pfx
DataProtection__CertificatePassword=$HCS_DATAPROTECTION_PFX_PASSWORD
EOF

connection_password_key=Password
printf 'ConnectionStrings__Default=Host=postgres;Port=5432;Database=hcs_identity;Username=hcs;%s=%s\n' "$connection_password_key" "$HCS_POSTGRES_PASSWORD" >>"$runtime_env"
printf 'ConnectionStrings__Organization=Host=postgres;Port=5432;Database=hcs_organization;Username=hcs;%s=%s\n' "$connection_password_key" "$HCS_POSTGRES_PASSWORD" >>"$runtime_env"
printf 'ConnectionStrings__DocumentService=Host=postgres;Port=5432;Database=hcs_document;Username=hcs;%s=%s\n' "$connection_password_key" "$HCS_POSTGRES_PASSWORD" >>"$runtime_env"
printf 'ConnectionStrings__WorkManagement=Host=postgres;Port=5432;Database=hcs_work;Username=hcs;%s=%s\n' "$connection_password_key" "$HCS_POSTGRES_PASSWORD" >>"$runtime_env"
printf 'ConnectionStrings__Collaboration=Host=postgres;Port=5432;Database=hcs_collaboration;Username=hcs;%s=%s\n' "$connection_password_key" "$HCS_POSTGRES_PASSWORD" >>"$runtime_env"
printf 'ConnectionStrings__BusManagement=Host=postgres;Port=5432;Database=hcs_bus_management;Username=hcs;%s=%s\n' "$connection_password_key" "$HCS_POSTGRES_PASSWORD" >>"$runtime_env"

kubectl create namespace "$namespace" --dry-run=client -o yaml | kubectl apply -f -
kubectl -n "$namespace" create secret generic hcs-runtime --from-env-file="$runtime_env" --dry-run=client -o yaml | kubectl apply -f -
kubectl -n "$namespace" create secret generic hcs-certificates \
  --from-file=openiddict.pfx="$HCS_OPENIDDICT_PFX" --from-file=dataprotection.pfx="$HCS_DATAPROTECTION_PFX" \
  --dry-run=client -o yaml | kubectl apply -f -

# An interrupted Helm --wait leaves a pending revision that rejects every
# later upgrade. This only recovers Helm metadata/resources; it preserves PVCs.
if helm -n "$namespace" status "$release" 2>/dev/null | grep -q 'STATUS: pending-'; then
  echo "Recovering interrupted Helm release '$release'..."
  helm -n "$namespace" rollback "$release" 1
fi

helm upgrade --install "$release" "$root_dir/deploy/helm/hcs-community" --namespace "$namespace" \
  --set ingress.host="$HCS_PUBLIC_HOST" --set ingress.authHost="$HCS_AUTH_PUBLIC_HOST" \
  --set-string images.authServer.repository="$image_prefix/auth-server" --set-string images.webGateway.repository="$image_prefix/web-gateway" \
  --set-string images.blazor.repository="$image_prefix/blazor" --set-string images.platform.repository="$image_prefix/platform" \
  --set-string images.organization.repository="$image_prefix/organization" --set-string images.document.repository="$image_prefix/document" \
  --set-string images.workManagement.repository="$image_prefix/work-management" --set-string images.collaboration.repository="$image_prefix/collaboration" \
  --set-string images.busManagement.repository="$image_prefix/bus-management" \
  --set-string images.dbMigrator.repository="$image_prefix/db-migrator" \
  --set ingress.tlsSecretName="$HCS_TLS_SECRET" --set-string deploymentNonce="$(date +%s)" \
  --set-string ingress.internalControllerIp="$ingress_controller_ip" \
  --wait --wait-for-jobs --timeout 12m

echo "HCS is deployed. Open https://$HCS_PUBLIC_HOST after DNS and TLS are available."
echo "Status: kubectl -n $namespace get pods,svc,ingress"
