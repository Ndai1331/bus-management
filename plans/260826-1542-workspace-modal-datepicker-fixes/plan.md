---
title: "Workspace modal parity and date-picker layering"
description: "Align the Workspace quick project detail modal with the canonical project-detail UX and keep the date-range popup above KPI cards."
status: completed
priority: P1
effort: 0.5-1d
branch: main
tags: [bugfix, frontend, blazor, workspace, ui]
blockedBy: []
blocks: []
created: 2026-08-26
createdBy: Codex
---

# Workspace modal parity and date-picker layering

## Overview

Fix two presentation-only regressions in the existing Blazor client:

1. Make the Workspace quick project detail modal visually and semantically follow the established `/project-detail/{id}` page.
2. Keep the Workspace date-range popup above the KPI/summary cards.

No route, API/DTO, authorization, data-flow, package, vendor CSS, or new application file is required. Keep the modal quick/read-only behavior and existing route/task actions.

## Baseline findings

- `Workspace.razor` lines 364-445 owns the quick project detail modal. It uses a custom `hcs-workspace-detail-grid`, summary labels, a bounded task list, member chips, and a generic modal title.
- `ProjectDetail.razor` lines 12-104 is the canonical reference: `hcs-document-page` header, `CardHeader` section titles, `hcs-detail-split`, general-information fields, `hcs-member-list`, and a full-width tasks card.
- Existing reusable styling is in `wwwroot/main.css`: `.hcs-catalog-form` (883+), `.hcs-member-list` (942+), `.hcs-detail-split` (1404+), and workspace detail rules (2584+). Shared tokens and modal primitives already exist in `hcs-tokens.css` and `hcs-components.css`.
- `Workspace.razor` lines 12-21 uses `HcsDatePicker` for the range filter; `HcsDatePicker.razor` wraps the existing Blazorise `DatePicker`. The workspace filter and KPI cards have no app-owned stacking layer. This makes the popup dependent on vendor popup z-index/load order and allows it to paint behind later summary-card content in the affected runtime.

## Related plans and dependencies

- The current `plans/` scan found no unfinished plan with this exact two-fix scope.
- The workspace/document parity plan outside this service plan directory has completed its Workspace quick-action phase; this is a follow-up polish plan and has no blocking dependency on its pending signing phases.
- Worktree overlap observed after the initial scan: an uncommitted diff now exists in both planned application files and already contains the requested modal/z-index direction. Treat it as user/parallel work, review and reconcile it before implementation; never reset or overwrite it.

## Phases

| Phase | Deliverable | Status |
|---|---|---|
| 1 | Canonical project-detail modal presentation | Completed |
| 2 | Workspace date-picker stacking fix | Completed |
| 3 | Build, browser smoke check, and diff boundary validation | Completed (automated; browser smoke unavailable) |

## Files

| Action | File | Scope |
|---|---|---|
| Modify | `src/HCS.Blazor.Client/Pages/Workspace.razor` | Modal markup/labels only; preserve existing load, error, task, and route callbacks. |
| Modify | `src/HCS.Blazor.Client/wwwroot/main.css` | Reuse canonical modal/detail classes and add scoped popup layering. |
| Verify only | `src/HCS.Blazor.Client/Pages/ProjectDetail.razor` | Source of truth for established project-detail layout and labels. |
| Verify only | `src/HCS.Blazor.Client/Components/HcsDatePicker.razor` | Confirm wrapper contract; do not add date-picker behavior. |
| Verify only | `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`, `hcs-components.css` | Reuse existing tokens/primitives; do not add a new design system layer. |
| Verify only | `src/HCS.Blazor/Components/App.razor` | Confirm CSS/bundle order if runtime stacking differs from source expectations. |

## Success criteria

- The quick project modal has the same section hierarchy, headers, spacing, status treatment, member-row treatment, and responsive collapse behavior as `/project-detail/{id}`, while remaining read-only and bounded.
- Existing project/task/file loading, error handling, permissions, callbacks, and “View detail” navigation are unchanged.
- The date-range popup visibly overlays the KPI/summary cards at desktop and mobile widths, remains clickable, and is not clipped by the workspace filter.
- Only the two existing application files above are changed during implementation; no new component or vendor file is created.
- Existing unrelated dirty files remain untouched; the planned delta is measured against the implementation-start baseline, not against a clean checkout.

## Completion notes

- Client build passed with 0 warnings and 0 errors.
- Related Work Management tests passed: 49/49.
- Scoped `git diff --check` passed for the two application files.
- Browser-level visual verification was not available in this environment; authenticated `/workspace` breakpoint checks remain a manual follow-up.

## Validation commands

Run from `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license`:

```bash
./scripts/audit-license-clean.sh
dotnet build src/HCS.Blazor/HCS.Blazor.csproj --no-restore
dotnet test HCS.slnx --no-build
git diff --check
git diff --name-only
```

Manual smoke check when the local host is available at `https://localhost:44403`:

- Open `/workspace`, open the quick project modal, compare it with `/project-detail/{id}` at desktop, tablet, and mobile widths; verify loading/error/empty states, Escape/close, task view, and route CTA.
- Open the date range picker while it overlaps the KPI row; verify the full calendar is above cards, selectable, keyboard reachable, and still works after selecting a range and pressing Search.

## Unresolved questions

- None blocking. If browser inspection shows a different rendered Blazorise popup class, record the computed class/z-index in the phase-02 report and keep the override scoped to `.hcs-workspace`.
