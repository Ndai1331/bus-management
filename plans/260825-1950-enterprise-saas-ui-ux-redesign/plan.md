---
title: "Enterprise SaaS UI/UX redesign foundation for HCS Blazor"
description: "Consolidate HCS tokens, shared styles, and the existing application shell without changing page behavior or contracts."
status: completed
progress: 100%
priority: P1
effort: 3-5d
branch: codex/redesign-ui
tags: [frontend, ui-ux, blazor, accessibility, shell]
blockedBy: []
blocks: []
created: 2026-08-25
---

# Enterprise SaaS UI/UX Redesign Foundation Plan

## Overview

Deliver a bounded presentation layer for the existing HCS Blazor frontend: one token source, consistent shared surfaces, and a clearer HCS shell over the current LeptonX/Bootstrap/Blazorise stack. This plan does not redesign individual business pages or change application behavior. It is deliberately limited to shell, tokens, and shared styles so it can be implemented and validated independently.

## Scan findings

The scan report is at [`plans/reports/260825-1950-blazor-frontend-scan.md`](../reports/260825-1950-blazor-frontend-scan.md). The relevant conclusions are:

- The client already uses .NET 10 Blazor WebAssembly with Blazorise 2.3.0, Bootstrap 5, FontAwesome, LeptonX Lite, and existing HCS CSS/JS seams.
- `hcs-tokens.css` and `hcs-components.css` are useful foundations, but `main.css`, scoped page styles, and LeptonX variables still contain competing color, radius, density, and surface decisions.
- `HCSMainLayout.razor` is the visible application shell even though LeptonX is configured with a side-menu layout. It owns route links, permission-aware groups, culture, notifications, chat, account actions, and the mobile drawer.
- `App.razor` already has an integration-sensitive stylesheet/script order. The plan therefore keeps it verify-only and makes the CSS changes in place.
- Feature pages, typed clients, BFF auth, authorization, SignalR, Select2, calendar/chart/PDF interop, and DTO/backend contracts are not implementation targets.

## Final implementation scope

### Exact application files to modify

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/main.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/hcs-components.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/CultureSelector.razor.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Components/GatewayDataPanel.razor.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Components/NotificationToast.razor.css`
 `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Domain.Shared/Localization/HCS/en.json`
 `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Domain.Shared/Localization/HCS/vi.json`
 `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/scripts/audit-navigation-layout.sh`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Components/UserSignaturesPanel.razor.css`

`/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor/Components/App.razor` is verify-only: confirm the existing LeptonX/HCS stylesheet order and scripts; do not change it unless a concrete CSS-loading defect is found and separately approved.

### Verify-only boundaries

Inspect but do not modify:

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Routes.razor`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Navigation/HCSMenuContributor.cs`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor/HCSBlazorModule.cs`
- localization resources and all files under `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Pages/`

### Explicitly out of scope

- Page-level redesign or markup changes under `Pages/**`; the pages are consumers of the shared primitives in this phase.
- Business logic, API calls, typed clients, DTOs/models, backend contracts, auth/BFF/antiforgery, authorization policies, route templates/parameters, query semantics, and localization behavior.
- Blazorise, Bootstrap, LeptonX, FontAwesome, Select2, SignalR, PDF, calendar, chart, or any other package/framework replacement.
- New services, new component frameworks, new JS interop, dark mode, new navigation destinations, and route cleanup.

## Design system direction

- Style: calm, light-first Swiss-minimal enterprise workspace; prioritize hierarchy, scanability, and restrained elevation over decoration.
- Color: preserve the HCS semantic blue family through token aliases; use the draft `#2563EB` direction only as a semantic mapping decision, not as scattered literals. Reserve orange for selective CTA emphasis and retain text-plus-color status semantics for success, warning, danger, and info.
- Typography: retain the existing Be Vietnam Pro stack by default for Vietnamese/English readability. Inter/Plus Jakarta Sans remains an explicit product decision, not a silent implementation change.
- Rhythm: keep a 4/8px spacing rhythm, consistent gutters, compact desktop data density, and 44px minimum interactive targets.
- Components: style existing Blazorise/Bootstrap controls through semantic and component tokens. Do not replace `DataGrid`, `Modal`, `Tabs`, `Field`, `Select`, `Button`, `NumericPicker`, or existing HCS helpers.
- Shell: maintain the current route hierarchy, permission-aware visibility, notifications, chat badge, language switching, account/logout actions, and mobile drawer while making active state, focus, spacing, and responsive behavior coherent. The user prompt explicitly selects a persistent 248px desktop rail with collapsed mode and a mobile off-canvas drawer.
- Accessibility: visible focus rings, named icon actions, keyboard-operable menus/drawers, color-plus-text status, live-region compatibility, and `scroll-padding-top`/sticky offsets that do not obscure focus.
- Motion: use only subtle 150–300ms transitions and preserve reduced-motion behavior.

## Non-negotiable guardrails

- Preserve every `@page` route, compatibility alias, route parameter, query parameter, `Authorize` attribute, `AuthorizeView` policy/role, anonymous survey route, and forbidden-state behavior.
- Preserve all injected clients, event handlers, API calls, DTO/model bindings, BFF navigation, antiforgery behavior, SignalR wiring, and JS interop.
- Preserve LeptonX side-menu configuration, Bootstrap 5, Blazorise 2.3.0 providers, FontAwesome, Select2, PDF viewer, calendar, chart, and existing script initialization.
- Prefer CSS and existing classes. Any shell markup change must be semantic/accessibility-only or a visual wrapper; it must not alter navigation destinations, authorization boundaries, or event behavior.
- Add no new global selector without checking LeptonX/Bootstrap specificity and existing `--hcs-*` consumers. Keep compatibility aliases when token names are reorganized.
- Keep localization behavior unchanged; no new shell copy is required by this plan.

## Phases

| Phase | Name | Status | Deliverable |
|---|---|---|---|
| 1 | Token foundation | Completed | [phase-01-foundations-shell.md](./phase-01-foundations-shell.md) |
| 2 | HCS shell and navigation styling | Completed | [phase-02-shared-surfaces.md](./phase-02-shared-surfaces.md) |
| 3 | Shared styles and validation | Completed | [phase-03-validation-rollout.md](./phase-03-validation-rollout.md) |

## Dependencies and sequencing

No package, backend, API, or external design dependency is required. The completed organization catalog plan at `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/plans/20260823-organization-catalog-parent-select2/plan.md` is not a blocker. Implement phases in order so shared tokens exist before shell and shared component styling is tuned.

## Success criteria

- The nine exact application files above are the only intended code changes.
- The shell and shared HCS primitives consume one semantic token layer without breaking existing `--hcs-*` consumers.
- Desktop, tablet, and mobile shell navigation retain all current destinations, permission gates, culture actions, notification/chat/account actions, and logout behavior.
- Shared data, loading, empty, error, modal, toast, and signature-panel surfaces use the same spacing, border, focus, and status language.
- Existing navigation/mobile audits pass; the solution builds and tests; manual checks cover 375px, 768px, 1024px, and 1440px plus keyboard and reduced-motion behavior.
- `git diff` shows no changes to pages, clients, models, auth, routes, module registration, backend, or JS files.

## Risks and mitigations

- **LeptonX/Bootstrap specificity:** keep the existing asset order, scope HCS overrides, and verify computed styles at shell and data-surface boundaries.
- **Sticky header and focus:** set consistent shell offsets, `scroll-padding-top`, focus-visible styles, drawer layers, and modal z-index; test direct deep links and keyboard traversal.
- **Token blast radius:** inventory all `--hcs-*` consumers, preserve aliases, and review screenshot diffs on representative existing pages without editing those pages.
- **Responsive overflow:** retain the existing mobile audit scripts and test long labels, dense tables, Select2 controls, notifications, and chat/account menus at narrow widths.
- **License/build noise:** do not alter dependencies; record any pre-existing Blazorise/license or environment failure separately from UI findings.

## Validation commands

Run from `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license`:

```bash
./scripts/audit-navigation-layout.sh
./scripts/audit-mobile-layout.sh
dotnet build HCS.sln --no-restore
dotnet test HCS.sln --no-restore --no-build
git diff --check
git diff --name-only
```

The final diff-name check must contain only the nine in-scope files (plus the plan/report files) and must not contain page business logic, client, model, auth, route, module, or JS changes. This checkout already has pre-existing changes in three of the in-scope shell/token files; compare the implementation diff with the pre-scan baseline so those changes are not attributed to this plan. If the solution has no `HCS.sln` at the repository root, use the existing solution path documented by `README.md` and record the exact command/result in the implementation handoff.

## Open decisions for implementation review

1. Be Vietnam Pro and the teal/navy HCS palette are the approved implementation direction; the persisted design-system master was synchronized to it.
2. The user prompt explicitly approves the persistent desktop rail and mobile off-canvas drawer.
3. Dark mode is excluded unless separately approved.
4. The existing Blazorise production-license decision remains a release concern and is not changed by this plan.

## Handoff

After review, start implementation with:

`/ck:cook /Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/plans/260825-1950-enterprise-saas-ui-ux-redesign/plan.md`
