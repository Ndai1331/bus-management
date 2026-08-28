#!/usr/bin/env bash
set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
solution_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
layout_css="$solution_root/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css"
layout_markup="$solution_root/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor"
tokens_css="$solution_root/src/HCS.Blazor.Client/wwwroot/hcs-tokens.css"

required_rules=(
    '.hcs-top-nav {'
    'display: flex;'
    'flex-wrap: wrap;'
    'overflow: visible;'
    'position: relative;'
    '.hcs-header__top {'
    'height: var(--hcs-header-height);'
    '.hcs-main-content {'
    'margin-left: 0;'
    '.hcs-nav-menu {'
    '.hcs-nav-menu__panel {'
    'position: absolute;'
    'top: calc(100% + .35rem);'
    'transform: translateY(-.25rem);'
    '.hcs-top-nav:not(.hcs-top-nav--open) {'
    'pointer-events: none;'
    'visibility: hidden;'
    '.hcs-top-nav--open {'
    'overscroll-behavior: contain;'
    'transform: translateX(-105%);'
    'transform: translateX(0);'
    '.hcs-nav-backdrop {'
    'position: fixed;'
    '.hcs-app-shell--nav-open .hcs-top-nav {'
    'transform: translateX(0);'
)

required_markup_rules=(
    'inert="@mobileNavOpen"'
    'hcs-app-shell__notifications'
)

required_resize_rules=(
    '.hcs-app-shell--nav-open .hcs-top-nav {'
    'position: fixed;'
    'top: var(--hcs-header-height);'
    'transform: translateX(0);'
)

required_routes=(
    'href="/workspace"'
    'href="/manage-documents?sourceType=0"'
    'href="/manage-documents?sourceType=1"'
    'href="/manage-documents?sourceType=2"'
    'href="/document-signing"'
    '("/workflow-definitions"'
    '("/workflow-lists"'
    '("/document-workflow-instances"'
    '("/projects"'
    '("/tasks"'
    'href="/calendar-events"'
    '("/survey-sessions"'
    '("/survey-results"'
    'href="/document-types"'
    'href="/sectors"'
    'href="/urgency-levels"'
    'href="/confidentiality-levels"'
    'href="/processing-methods"'
    'href="/document-status"'
    'href="/signing-methods"'
    'href="/signature-settings"'
    'href="/event-types"'
    'href="/departments"'
    'href="/unit-lists"'
    'href="/positions"'
    'href="/survey-locations"'
    'href="/survey-criterias"'
    'href="/reports"'
    '("/administration"'
    '("/administration/roles"'
    '("/administration/languages"'
    '("/administration/audit-logs"'
    'href="/chat"'
)

required_access_guards=(
    'Policy="Documents.View"'
    'Policy="Documents.Assign"'
    'Policy="Documents.Signing.Execute"'
    'Policy="HCS.Organization.MasterData"'
    'Policy="HCS.Organization.Departments"'
    'Policy="HCS.Organization.Units"'
    'Policy="HCS.Organization.Positions"'
    'Policy="Collaboration.Chat"'
    'Roles="admin"'
)

required_color_tokens=(
    '--color-primary: #00B4A9;'
    '--color-secondary: #007F7C;'
    '--color-primary-dark: #007F7C;'
    '--color-primary-light: #E0F7F5;'
    '--color-teal-top: #007F7C;'
    '--color-accent: #E31E24;'
    '--color-text: #1A1A2E;'
    '--color-muted: #5C6578;'
    '--color-border: #DDE4EE;'
    '--color-bg: #F5F8FC;'
)

for rule in "${required_rules[@]}"; do
    if ! grep -Fq -- "$rule" "$layout_css"; then
        printf 'ERROR: missing top-menu navigation layout guard: %s\n' "$rule" >&2
        exit 1
    fi
done

for rule in "${required_markup_rules[@]}"; do
    if ! grep -Fq -- "$rule" "$layout_markup"; then
        printf 'ERROR: missing mobile focus-isolation guard: %s\n' "$rule" >&2
        exit 1
    fi
done

for rule in "${required_resize_rules[@]}"; do
    if ! grep -Fq -- "$rule" "$layout_css"; then
        printf 'ERROR: missing desktop-resize navigation guard: %s\n' "$rule" >&2
        exit 1
    fi
done

for route in "${required_routes[@]}"; do
    if ! grep -Fq -- "$route" "$layout_markup"; then
        printf 'ERROR: missing preserved navigation route: %s\n' "$route" >&2
        exit 1
    fi
done

for guard in "${required_access_guards[@]}"; do
    if ! grep -Fq -- "$guard" "$layout_markup"; then
        printf 'ERROR: missing preserved authorization guard: %s\n' "$guard" >&2
        exit 1
    fi
done

for token in "${required_color_tokens[@]}"; do
    if ! grep -Fq -- "$token" "$tokens_css"; then
        printf 'ERROR: missing canonical color token: %s\n' "$token" >&2
        exit 1
    fi
done

if grep -Eq 'sidebarCollapsed|hcs-sidebar|hcs-app-shell--sidebar|hcs-sidebar-current-width|margin-left: var\(--hcs-sidebar' "$layout_markup" "$layout_css"; then
    printf 'ERROR: desktop sidebar state or spacing remains in the top-menu shell\n' >&2
    exit 1
fi

if grep -Fq 'justify-content: flex-center;' "$layout_css"; then
    printf 'ERROR: invalid flex alignment value remains in navigation CSS\n' >&2
    exit 1
fi

printf 'Desktop navigation alignment audit passed.\n'
