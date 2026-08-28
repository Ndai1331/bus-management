---
title: "Blazor frontend scan for enterprise SaaS UI/UX redesign"
description: "Evidence-backed inventory of the HCS Blazor UI, its design-system seams, and redesign constraints."
status: completed
created: 2026-08-25
tags: [frontend, ui-ux, blazor, leptonx, blazorise]
---

# Blazor Frontend Scan Report

## Summary

The frontend is a .NET 10 Blazor WebAssembly client hosted by a .NET 10 Blazor server project. It already has a substantial HCS-specific visual layer and a custom two-row application shell. The redesign should consolidate and extend that layer rather than introduce a second UI framework or rewrite feature behavior.

No application code was modified during this scan. The worktree currently also contains pre-existing modifications in `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor`, `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css`, and `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`; those changes were not made or reviewed as implementation work here. The untracked `design-system/hcs-enterprise-workspace/MASTER.md` was read as a draft input and was not changed.

## Sources read

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/README.md`
- User-provided repository `AGENTS.md` instructions
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/docs/dependency-license-decisions.md`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/docs/handoff-2026-08-08.md`
- Existing completed plan `plans/20260823-organization-catalog-parent-select2/plan.md`
- `src/HCS.Blazor.Client` and `src/HCS.Blazor` project/startup/layout/navigation/page/component/style files
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/design-system/hcs-enterprise-workspace/MASTER.md`

The repository does not currently contain `docs/development-rules.md`, `docs/codebase-summary.md`, `docs/code-standards.md`, or `docs/design-guidelines.md`; the plan therefore uses the README, supplied AGENTS rules, existing code patterns, and existing audit scripts as the source of truth.

## Frontend architecture

| Area | Evidence | Implication |
|---|---|---|
| Client | `src/HCS.Blazor.Client/HCS.Blazor.Client.csproj` targets `net10.0` and references Blazorise 2.3.0, Bootstrap 5, FontAwesome, PDF viewer, SignalR, ABP Identity/Feature/Setting modules, and LeptonX Lite WebAssembly theme. | Keep the package set and component APIs. Do not introduce another component framework. |
| Host | `src/HCS.Blazor/HCS.Blazor.csproj` references the server LeptonX Lite theme, MVC theme, WebAssembly bundling, and Blazorise Bootstrap 5 providers. | Keep LeptonX styles/scripts and bundle registration. Apply HCS overrides in the existing asset order. |
| App document | `src/HCS.Blazor/Components/App.razor` loads LeptonX bundles, scoped styles, `hcs-tokens.css`, `main.css`, `hcs-components.css`, Select2, PDF CSS, and HCS JS. | CSS order and z-index are integration-sensitive. Any new stylesheet must be deliberate; in-place token refactoring is lower risk. |
| Routing/auth | `src/HCS.Blazor.Client/Routes.razor`, `BffAuthenticationGate.razor`, `AuthorizeRouteView`, page `[Authorize]` attributes, and `AuthorizeView` blocks enforce access. | Preserve every route, role, policy, anonymous survey route, BFF redirect, and forbidden state. |
| Navigation | `HCSMainLayout.razor` renders the visible custom menu. `HCSMenuContributor.cs` also contributes ABP menu metadata. | There are two navigation representations. Keep them aligned if information architecture changes. |

## Existing UI and design seams

1. **Strong reusable base already exists.** `hcs-tokens.css` defines colors, typography, spacing, breakpoints, radii, shadows, z-index, motion, focus, and safe-area variables. `hcs-components.css` defines page, filter, data surface, state, responsive table, and accessibility primitives.
2. **Global styling is split.** `main.css` contains LeptonX/Bootstrap compatibility plus catalog CRUD, modal, calendar, data grid, badge, workflow, and page-family rules. Page-scoped CSS adds separate visual systems for account, admin, roles, chat, survey, and signatures.
3. **The shell is custom HCS UI over LeptonX.** `HCSMainLayout.razor` has a sticky two-row header, top-level links, hover/click dropdowns, a mobile accordion drawer, language switcher, notifications, chat badge, and account menu. The module still configures LeptonX `side-menu`, but the visible route shell is the HCS layout.
4. **The product is data-heavy.** Catalog, document, workflow, project, survey, administration, and report pages use Blazorise `DataGrid`, `Card`, `Modal`, `Tabs`, `Field`, `Select`, `Button`, `NumericPicker`, and PDF components. Many pages already share `hcs-catalog-page`, `hcs-catalog-filter-card`, `hcs-catalog-grid-card`, `hcs-catalog-modal`, and state classes.
5. **Complex experiences need conservative treatment.** Chat is 1,479 lines with SignalR/retry/attachment/pin/forward/member interactions; `DocumentDetail`, `DocumentSigning`, `WorkflowDetail`, and project detail pages embed substantial stateful forms and modals. Visual changes should be markup/CSS-only around existing handlers.
6. **Existing responsive work is valuable.** `scripts/audit-mobile-layout.sh` checks data-surface overflow containment and `scripts/audit-navigation-layout.sh` checks desktop navigation alignment. Preserve and extend these checks.

## Route and authorization inventory

- Workspace: `/`, `/workspace`; dashboard permission on `/workspace`; anonymous entry redirects authenticated users to `/workspace`.
- Documents/signing: `/manage-documents`, `/my-documents`, `/document-assignments`, `/document-files`, `/document-histories`, `/document-detail`, `/document-signing`, workflow routes, signing settings, and KPI report.
- Organization/catalogs: `/departments`, `/unit-lists`, `/positions`, `/master-datas`, typed master-data aliases, and the `/even-types` compatibility alias.
- Work: projects, tasks, calendar, reports, surveys, and public `/survey-collections/{id}`.
- Collaboration: `/chat`, `/chat/{id}`, compatibility `/chat1` routes, and `/notifications`.
- Administration: `/administration`, `/users`, `/identity/users-management`, `/administration/roles`, `/roles`, language/text/audit-log feature routes; admin-role protected.
- Account: `/account` and `/user-signatures`.

The exact per-page authorization attributes are listed by the implementation plan and must remain unchanged.

## API/auth/business-logic boundary

The UI injects typed clients including `AccountProfileClient`, `CollaborationClient`, `DocumentClient`, `WorkManagementClient`, `OrganizationCatalogClient`, and `IdentityAdminClient`. Authentication is handled by `BffAuthenticationStateProvider` and `BffHttpMessageHandler`; unsafe requests receive the existing antiforgery handling. Chat uses `ChatRealtimeConnection` and `hcs-chat.js`; Select2 uses `CatalogSelect2`/`UserSelect2` and `hcs-catalog-select2.js`; calendar and chart components use HCS JS interop.

The redesign must not change these clients, DTOs/models, endpoint paths, request payloads, SignalR events, BFF cookies, antiforgery behavior, authorization policy names, or route parameters/query semantics.

## Design-system input and mismatch

The untracked draft `design-system/hcs-enterprise-workspace/MASTER.md` proposes Inter, `#2563EB` primary, orange CTA, `#F8FAFC` background, 8px radius, and a minimal style. The current app uses Be Vietnam Pro, `#3D5CFF`, `#F5F8F7`, 5px global corners, and several local hard-coded colors. The implementation plan recommends an accessible, light-first Swiss/minimal enterprise workspace: retain Be Vietnam Pro by default for Vietnamese readability, map the current HCS tokens to a blue-600-compatible semantic scale, reserve orange for selective CTA emphasis, and remove ad-hoc per-page color decisions. Font replacement remains an explicit product decision, not an inferred technical requirement.

## Scope challenge

- Existing code: reusable shell, tokens, page primitives, data-grid patterns, state templates, modal patterns, responsive guards, and audit scripts already solve most infrastructure needs.
- Final implementation boundary: token consolidation, the existing HCS shell/navigation, and shared CSS primitives only. Domain pages remain scan context and validation consumers; they are not implementation targets in this plan.
- Exact application change set: nine existing CSS/Razor files across the token layer, shared styles, shell, culture control, and shared UI components. No new services, DTOs, components, packages, routes, or API calls.
- Complexity: approximately 3–5 implementation days, with the main risk concentrated in CSS specificity, sticky-shell focus behavior, and responsive containment.
- Selected mode: **HOLD SCOPE**. The broader page-family redesign is intentionally deferred so this plan can be implemented and validated as a bounded presentation foundation.

## Implementation scope finalized

### In scope

- `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`: primitive, semantic, and component tokens with compatibility aliases for existing `--hcs-*` consumers.
- `src/HCS.Blazor.Client/wwwroot/main.css`: Bootstrap/LeptonX bridge and shared visual overrides migrated to tokens.
- `src/HCS.Blazor.Client/wwwroot/hcs-components.css`: page, filter, data-surface, state, responsive, focus, and utility primitives.
- `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor` and `.razor.css`: shell landmarks, active-state presentation, header actions, mobile drawer presentation, focus order, and responsive offsets; preserve all existing links and permission checks.
- `src/HCS.Blazor.Client/Layouts/CultureSelector.razor.css`: shared control styling only; preserve the existing culture persistence and keyboard behavior.
- `src/HCS.Blazor.Client/Components/GatewayDataPanel.razor.css`, `NotificationToast.razor.css`, and `UserSignaturesPanel.razor.css`: align shared component states and surfaces to the token system.

### Verify only

- `src/HCS.Blazor/Components/App.razor`: confirm stylesheet order and existing scripts; no asset or script change is planned.
- `src/HCS.Blazor.Client/Routes.razor`, `HCSMenuContributor.cs`, `src/HCS.Blazor/HCSBlazorModule.cs`, localization resources, and all page files: verify route/auth/menu/bundle compatibility; do not modify.

### Explicitly out of scope

- All page-level Razor/CSS redesigns under `src/HCS.Blazor.Client/Pages/**`.
- Typed clients, services, DTOs/models, API calls/contracts, auth/BFF/antiforgery, authorization policies, route parameters, SignalR, Select2, calendar/chart/PDF JS interop, and package/framework changes.

## Unresolved questions

1. Approve the draft palette/font direction: keep Be Vietnam Pro with HCS blue aliases, or adopt Inter/Plus Jakarta Sans and the draft palette globally?
2. Should the desktop target be a persistent left navigation rail, or should the existing two-row top navigation remain the primary pattern with only hierarchy/density improvements?
3. Is dark mode part of this redesign? The current app has no documented theme switch; adding it would expand token and validation scope.
4. Is the Blazorise commercial license decision resolved for production? The existing dependency decision says Blazorise 2.3.0 remains a release blocker.
