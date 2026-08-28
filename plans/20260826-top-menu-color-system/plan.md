---
title: "HCS desktop top menu and exact color system"
description: "Move desktop navigation from the current fixed sidebar to a horizontal top menu while retaining the <=1100px mobile drawer and all application contracts."
status: completed
progress: 100%
priority: P1
effort: 1-2d
branch: codex/redesign-ui
tags: [frontend, feature, accessibility, ui]
blockedBy: []
blocks: []
created: 2026-08-26
---

# HCS desktop top menu and exact color system

## Overview

Convert the current HCS Blazor shell from a fixed desktop sidebar to a two-row desktop header with a horizontal primary menu. Keep the existing mobile off-canvas drawer at `max-width: 1100px`, and replace the dominant visual tokens with the ten exact `--color-*` values requested by the user. This is presentation-layer work only; no routes, authorization, authentication, API/DTO, backend, or framework contracts change.

The current worktree already contains uncommitted UI changes in the target shell, token, component, and audit files. Implementation must review the existing diff first and preserve unrelated user changes.

## Exact implementation files

Modify only these application files:

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/hcs-components.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/main.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/scripts/audit-navigation-layout.sh`

Verify only; do not modify: `src/HCS.Blazor.Client/Routes.razor`, `src/HCS.Blazor.Client/Navigation/HCSMenuContributor.cs`, `src/HCS.Blazor/Components/App.razor`, all `Pages/**`, typed clients, models/DTOs, auth/BFF code, backend services, localization, package/framework files, and JavaScript.

## Contract guardrails

- Keep every existing `NavLink`, `href`, query string, `NavLinkMatch`, `AuthorizeView` policy/role, and `Collaboration.Chat` guard unchanged.
- Preserve the current destinations: workspace; document list/my documents/sent documents/signing; workflow definitions/lists/instances; projects/tasks; calendar; surveys; organization/document/survey catalogs; reports; admin; and chat.
- Preserve `Navigation.LocationChanged`, `Account.AvatarChanged`, avatar fallback/loading, notification and chat unread callbacks, `NotificationToast`, `CultureSelector`, account menu, BFF logout construction, and `forceLoad` behavior.
- Preserve the existing mobile focus isolation (`inert`/`aria-hidden`), drawer backdrop, Escape handling, route-change close/reset, and focus return to `navToggle`.
- Do not add new navigation destinations, permission checks, services, components, JavaScript, or responsive breakpoints beyond the explicit `1100px` shell boundary.

## Implementation phases

### 1. Canonical tokens and compatibility aliases

Make these the only canonical dominant palette variables, with the exact spelling and values shown:

```css
--color-primary: #00B4A9;
--color-secondary: #007F7C;
--color-primary-dark: #007F7C;
--color-primary-light: #E0F7F5;
--color-teal-top: #007F7C;
--color-accent: #E31E24;
--color-text: #1A1A2E;
--color-muted: #5C6578;
--color-border: #DDE4EE;
--color-bg: #F5F8FC;
```

Retain existing `--hcs-*` names as compatibility aliases so current component/page CSS continues to resolve:

| Existing consumer token | Compatibility mapping |
|---|---|
| `--hcs-color-primary` | `var(--color-primary)` |
| `--hcs-color-primary-strong` | `var(--color-primary-dark)` |
| `--hcs-color-primary-light` | `var(--color-primary-light)` |
| `--hcs-color-primary-soft` | `var(--color-primary-light)` |
| `--hcs-color-sidebar` | `var(--color-teal-top)` |
| `--hcs-color-ink` | `var(--color-text)` |
| `--hcs-color-muted` | `var(--color-muted)` |
| `--hcs-color-background` | `var(--color-bg)` |
| `--hcs-color-border` | `var(--color-border)` |
| `--hcs-color-danger` | `var(--color-accent)` |
| `--hcs-color-success` | `var(--color-secondary)` |
| `--hcs-color-info` | `var(--color-primary)` |

Keep white as the existing neutral surface (`--hcs-color-surface`) and use the existing semantic status aliases only where required by status UI; do not introduce additional `--color-*` palette names. Map warning/status treatments deliberately to the permitted accent/teal colors and retain text/icon labels so status is not communicated by color alone. Update Bootstrap-compatible variables in `main.css` from the HCS aliases, not from duplicated hex literals. Replace dominant old blue/navy/gray literals in `main.css`, `hcs-components.css`, and shell CSS with aliases; preserve structural white, transparent, black, and shadow-alpha values where they are not palette tokens.

### 2. Desktop shell and responsive navigation

- In `HCSMainLayout.razor`, remove only sidebar-specific presentation state and controls (`sidebarCollapsed`, the collapse button, `ToggleSidebar`, and sidebar class wiring) if no longer needed after the CSS conversion. Leave all menu markup, callbacks, permission wrappers, and route strings intact.
- At widths above `1100px`, render `.hcs-header__top` as the brand/action row and `.hcs-top-nav` as a normal horizontal second row. Remove fixed positioning, sidebar width variables, header/content left margins, collapsed-label rules, and sidebar flyout behavior.
- Keep direct links and dropdown triggers on one flex track with a stable trigger anchor. Use a horizontally scrollable nav row only when necessary to avoid page-level horizontal overflow; dropdown panels remain positioned relative to their own menu item and constrained to the viewport.
- Keep desktop menu interaction accessible: click toggles existing sections, pointer hover may open/close sibling sections, active `NavLink`/menu state remains visible, `:focus-visible` remains explicit, and Escape closes an expanded menu/user menu.
- At `max-width: 1100px` (including exactly `1100px`), retain the current drawer behavior: header-only top row, visible hamburger, hidden sidebar-collapse control, fixed drawer below the header, `translateX` closed state, backdrop, `pointer-events`/`visibility` isolation, safe-area padding, and zero content left margin.
- Keep mobile submenu panels as the existing accordion behavior; selecting a link closes the drawer and returns focus to the toggle. Preserve chat full-height containment and notification layering.

### 3. Shared CSS cleanup and audit update

- In `hcs-components.css` and `main.css`, make focus rings, page surfaces, forms, cards, tables, alerts, badges, buttons, boot/error states, and responsive utilities consume the canonical aliases.
- Preserve `min-width: 0`, data-surface/table horizontal scrolling, 44px touch targets, 16px mobile inputs, `100dvh`, safe-area insets, `:has(.hcs-chat-page)` containment, and reduced-motion rules.
- Rewrite `scripts/audit-navigation-layout.sh` so its positive assertions describe the desktop horizontal menu (normal/static layout, shared flex track, dropdown anchoring, no sidebar margins) and the `<=1100px` drawer (fixed/transform/visibility/backdrop/focus isolation). Remove assertions that require sidebar width, left margins, collapsed-sidebar flyouts, or sidebar wording.

## Validation commands

Run from `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license`:

```bash
./scripts/audit-license-clean.sh
./scripts/audit-navigation-layout.sh
./scripts/audit-mobile-layout.sh
dotnet build HCS.slnx --no-restore
dotnet test HCS.slnx --no-build
git diff --check
git diff --name-only
```

Before implementation, record the dirty-worktree baseline. After implementation, `git diff --name-only` may contain only the six application files above plus this plan/report (and pre-existing user files); it must not contain route, page business logic, client, model, auth, backend, package, or JavaScript changes.

Manual shell checks, when the local host is available, must cover `1440px` and `1101px` desktop top menus, exactly `1100px`, `768px`, and `375px` mobile drawers, keyboard tab/Escape/focus return, active nested routes/query strings, long localized labels, notification/user menus, chat full-height layout, reduced motion, and no page-level horizontal overflow.

## Risks and mitigations

- **Dirty worktree overlap:** inspect and preserve the pre-existing diff; do not reset or rewrite unrelated hunks.
- **CSS specificity/load order:** keep scoped shell selectors and existing stylesheet order; use stable `.hcs-*` wrappers and `::deep` only where Blazor component rendering requires it. Verify computed styles against Bootstrap/Blazorise/LeptonX output.
- **Dropdown clipping/overflow:** anchor each panel to its trigger, constrain wide catalog panels to the viewport, and test at `1101px` with long Vietnamese/English labels.
- **Permission or route regression:** do a markup diff check for every `NavLink`, `href`, policy, role, query string, and `NavLinkMatch`; do not refactor the permission tree.
- **Accessibility regression:** preserve focus-visible styles, drawer focus return, Escape close, inert content isolation, named icon controls, touch targets, and text-plus-icon status semantics.
- **Token blast radius:** keep all `--hcs-*` aliases, inventory remaining old literals, and review representative workspace/catalog/document/chat/account surfaces.
- **Build/test environment noise:** report pre-existing restore/license/infrastructure failures separately; do not change dependencies or license configuration as part of this work.

## Plan review verdict

Approved for implementation as a bounded presentation-layer follow-up. The requested `1100px` breakpoint takes precedence over the older generic design-guideline breakpoints for this shell. No cross-plan blocker was found: the two prior HCS UI redesign plans are completed, and this plan intentionally supersedes only their sidebar presentation direction. No application code was changed while creating or reviewing this plan.

## Implementation status

Completed on 2026-08-26; progress is 100%. Desktop navigation now uses a two-row horizontal top menu above 1100px; the existing drawer remains active at and below 1100px. The requested ten canonical color tokens and `--hcs-*` compatibility aliases are in place. Routes, query strings, authorization guards, authentication callbacks, API/DTO contracts, and backend code were preserved.

Verification completed:

- `./scripts/audit-license-clean.sh` — PASS.
- `./scripts/audit-navigation-layout.sh` — PASS, including token, route, policy/role, mobile focus-isolation, and desktop-resize assertions.
- `./scripts/audit-mobile-layout.sh` — PASS.
- `dotnet build HCS.slnx --no-restore` — PASS, 0 warnings and 0 errors.
- `dotnet test HCS.slnx --no-build` — PASS, 332 passed, 0 failed, 0 skipped.
- `git diff --check` — PASS.
- [`20260826-top-menu-color-system-qa.md`](../reports/20260826-top-menu-color-system-qa.md) — PASS, 53/53 static top-menu/color-token assertions.
- Reviewer evidence — the plan review verdict is approved; [`20260825-final-hcs-enterprise-ui-redesign-code-review-r3.md`](../reports/20260825-final-hcs-enterprise-ui-redesign-code-review-r3.md) is marked `DONE`.

Known limitation: browser/manual viewport and keyboard smoke checks were not completed because `https://localhost:44403` was unavailable (`HTTP 000`). [`20260826-top-menu-color-system-debug.md`](../reports/20260826-top-menu-color-system-debug.md) records residual source-level risks R1–R9 for follow-up; these do not invalidate the completed automated gates.
