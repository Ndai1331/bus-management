#!/usr/bin/env bash
set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
solution_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
components_css="$solution_root/src/HCS.Blazor.Client/wwwroot/hcs-components.css"

required_selectors=(
    '.hcs-app-shell .hcs-document-layout > *,'
    '.hcs-app-shell .hcs-catalog-grid-card > .card-body,'
    '.hcs-app-shell .hcs-catalog-grid-card .table-responsive'
)

for selector in "${required_selectors[@]}"; do
    if ! grep -Fq "$selector" "$components_css"; then
        printf 'ERROR: missing mobile overflow guard: %s\n' "$selector" >&2
        exit 1
    fi
done

if ! grep -Fq 'overflow-x: auto;' "$components_css"; then
    printf 'ERROR: data-surface horizontal containment is missing\n' >&2
    exit 1
fi

printf 'Mobile layout containment audit passed.\n'
