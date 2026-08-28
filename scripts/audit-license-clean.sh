#!/usr/bin/env bash
set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
solution_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
failed=0

scan_files() {
  find "$solution_root" -type f \
    ! -path '*/.git/*' \
    ! -path '*/.abpstudio/*' \
    ! -path '*/.runtime-build/*' \
    ! -path '*/bin/*' \
    ! -path '*/obj/*' \
    ! -path "$solution_root/scripts/audit-license-clean.sh" \
    ! -name '*LicenseBoundaryTests.cs' \
    ! -name '*.db' ! -name '*.db-shm' ! -name '*.db-wal' \
    -print0
}

report_matches() {
  local title=$1
  local pattern=$2
  local matches
  matches=$(scan_files | xargs -0 grep -nE "$pattern" 2>/dev/null || true)
  if [[ -n "$matches" ]]; then
    printf 'ERROR: %s\n%s\n' "$title" "$matches" >&2
    failed=1
  fi
}

report_matches \
  'commercial or excluded ABP dependency detected' \
  'Volo\.Abp\.(Commercial|Saas|Gdpr|LanguageManagement|TextTemplateManagement|FileManagement|Forms|Studio)|Volo\.Saas|Volo\.FileManagement|Volo\.Forms|\.Pro([.<"/]|$)|AbpStudio\(|nuget\.abp\.io'

resolved_matches=$(find "$solution_root" -path '*/obj/project.assets.json' -type f \
  -exec grep -nHE '"(Volo\.Abp\.(Commercial|Saas|Gdpr|LanguageManagement|TextTemplateManagement|FileManagement|Forms|Studio)|Volo\.Saas|Volo\.FileManagement|Volo\.Forms|[^"]+\.Pro)/' {} + \
  2>/dev/null || true)
if [[ -n "$resolved_matches" ]]; then
  printf 'ERROR: resolved commercial or excluded dependency detected\n%s\n' "$resolved_matches" >&2
  failed=1
fi

report_matches \
  'NuGet vulnerability auditing is disabled' \
  '<NuGetAudit>false</NuGetAudit>'

report_matches \
  'private key material detected' \
  'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY'

certificate_files=$(find "$solution_root" -type f \
  ! -path '*/.git/*' ! -path '*/bin/*' ! -path '*/obj/*' ! -path '*/.runtime-build/*' \
  \( -name '*.pfx' -o -name '*.p12' -o -name '*.key' \) -print || true)
if [[ -n "$certificate_files" ]]; then
  printf 'ERROR: certificate/private-key file detected; mount it at runtime instead\n%s\n' "$certificate_files" >&2
  failed=1
fi

generated_abp_artifacts=$(find "$solution_root" -type f \
  ! -path '*/bin/*' ! -path '*/obj/*' ! -path '*/.runtime-build/*' \
  \( -name '*.abppkg' -o -name '*.abppkg.analyze.json' \) -print || true)
if [[ -n "$generated_abp_artifacts" ]]; then
  printf 'ERROR: generated ABP Studio artifact detected\n%s\n' "$generated_abp_artifacts" >&2
  failed=1
fi

report_matches \
  'certificate is configured as source/build content' \
  '(EmbeddedResource|Content|None)[^>]+Include="[^"]+\.(pfx|p12|key)"'

report_matches \
  'known development credential detected' \
  '1q2w3E\*'

connection_candidates=$(scan_files | xargs -0 grep -nE '(^|[;"[:space:]])(Password|Pwd)=[^;"[:space:]]+' 2>/dev/null || true)
connection_candidates=$(printf '%s\n' "$connection_candidates" | grep -Eiv '(Password|Pwd)=(\.\.\.|<[^>]+>|placeholder|change-me|development-only|\$\{[^}]+\})' || true)
if [[ -n "$connection_candidates" ]]; then
  printf 'ERROR: password-bearing connection string detected\n%s\n' "$connection_candidates" >&2
  failed=1
fi

secret_candidates=$(scan_files | xargs -0 grep -nE '"(ClientSecret|SecretKey|AccessKey|PassPhrase|Password)"[[:space:]]*:[[:space:]]*"[^"$<{]+' 2>/dev/null || true)
secret_candidates=$(printf '%s\n' "$secret_candidates" | grep -Eiv '(placeholder|change-me|development-only|example|localhost)' || true)
if [[ -n "$secret_candidates" ]]; then
  printf 'ERROR: possible literal secret detected\n%s\n' "$secret_candidates" >&2
  failed=1
fi

sources=$(grep -E '<add key=' "$solution_root/NuGet.Config" | sed -E 's/.*value="([^"]+)".*/\1/')
if [[ "$sources" != 'https://api.nuget.org/v3/index.json' ]]; then
  printf 'ERROR: NuGet.Config must contain only nuget.org\n%s\n' "$sources" >&2
  failed=1
fi

if [[ $failed -ne 0 ]]; then
  exit 1
fi

printf 'License and secret audit passed.\n'
