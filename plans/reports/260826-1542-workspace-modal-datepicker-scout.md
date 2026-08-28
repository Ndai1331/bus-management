---
title: "Scout report — Workspace modal and date-picker UI fixes"
type: scout
status: complete
created: 2026-08-26
scope: [workspace, project-detail, date-picker, css-layering]
---

# Scout Report

## Summary

The two fixes are bounded to existing Blazor markup and the application stylesheet. No new component, API contract, localization key, package, or vendor asset is needed.

## Relevant files

- `src/HCS.Blazor.Client/Pages/Workspace.razor` — workspace filter, KPI cards, quick project modal, task/member callbacks.
- `src/HCS.Blazor.Client/Pages/ProjectDetail.razor` — canonical `/project-detail` structure and reusable UI class pattern.
- `src/HCS.Blazor.Client/Components/HcsDatePicker.razor` — existing Blazorise DatePicker wrapper; range behavior is already configured by the page.
- `src/HCS.Blazor.Client/wwwroot/main.css` — domain styles for modal forms, detail split, member list, workspace cards, and current date-picker background override.
- `src/HCS.Blazor.Client/wwwroot/hcs-components.css` — shared card/modal/token consumers; no workspace-specific popup layer.
- `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css` — existing z-index scale, including `--hcs-z-popover: 350`.
- `src/HCS.Blazor/Components/App.razor` — stylesheet/bundle load order to verify if runtime computed styles disagree with source inspection.

## Findings

### Project modal

`Workspace.razor:364-445` uses `hcs-catalog-modal` but renders a custom summary layout: generic title, `hcs-workspace-detail-grid`, inline metadata labels, bounded task rows, member chips, and a footer route button. The canonical `ProjectDetail.razor:12-104` uses the document-page/detail pattern, explicit `CardHeader` sections, `hcs-detail-split`, localized field grouping, `hcs-member-list`, and a full task section. The quick modal can adopt that visual hierarchy while retaining read-only/bounded behavior.

### Date picker

`Workspace.razor:12-21` wraps the range picker in `.hcs-ws-filter-dates`. `main.css:2028-2054` defines the filter geometry, and `main.css:2082-2096` defines the following KPI cards, but neither establishes a page-local stacking layer. The current custom date rule only sets the calendar background (`main.css:2531-2533`). Blazorise Bootstrap5 2.3.0’s packaged stylesheet defines `.datepicker.show`/`.datepicker-calendar.dropdown-menu` z-index values, but the reported symptom still warrants a scoped application rule because the effective runtime layer depends on bundle order and ancestor stacking.

## Recommended boundary

- Modify `Workspace.razor` only in the project-detail modal markup.
- Modify `wwwroot/main.css` for canonical quick-modal selectors and the `.hcs-workspace` date-popup layer.
- Verify `ProjectDetail.razor`, `HcsDatePicker.razor`, tokens, shared CSS, and `App.razor`; do not modify them unless browser evidence disproves the plan.
- Preserve all current typed clients, DTOs, route/permission attributes, callbacks, vendor assets, and existing user worktree changes.

## Worktree overlap observed after the initial scan

The final read-only status/diff check shows uncommitted changes in both planned application files. The diff already adds the canonical modal class/title/member-list structure and workspace filter/date-popup z-index rules, and removes the old custom detail-grid/member-chip rules. These changes appeared during the shared worktree session; this planning turn did not edit application files. Future implementation must review those hunks as user/parallel work and use the validation phase to reconcile behavior, link semantics, and CSS specificity.

## Unresolved questions

- The exact generated date-picker class and computed ancestor overflow must be confirmed during browser smoke testing; the plan includes a selector fallback without widening scope.
