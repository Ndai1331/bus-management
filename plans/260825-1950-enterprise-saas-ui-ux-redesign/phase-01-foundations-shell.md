---
status: completed
progress: 100%
---

# Phase 1 — Token foundation

## Objective

Make the existing HCS token layer the single visual source for shell and shared controls. This phase changes CSS only; it does not change page markup, behavior, routes, or component registration.

## Exact files

Modify:

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/main.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/hcs-components.css`

Verify only:

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor/Components/App.razor` — confirm current CSS order; no change planned.
- Existing page-scoped CSS and `src/HCS.Blazor.Client/Pages/**` — inventory consumers and regression targets; do not modify.

## Implementation steps

1. Inventory every current `--hcs-*` variable and its consumers before renaming or regrouping anything.
2. Keep existing public aliases while organizing tokens into primitive, semantic, and component layers: color, typography, spacing, radius, shadow, z-index, motion, focus, control height, and responsive breakpoints.
3. Define semantic surfaces/text/borders/status and component tokens for buttons, fields, cards, tables, menus, modals, badges, and focus rings. Use the current HCS blue as the compatibility baseline; do not add unreviewed raw colors.
4. Replace raw colors, radii, and repeated spacing in `main.css` and `hcs-components.css` with token references while preserving Bootstrap/LeptonX variable bridges and existing catalog/calendar/modal/data-grid selectors.
5. Scope HCS overrides under existing HCS shell/component classes wherever possible. Avoid broad LeptonX selector overrides unless they are an explicit variable bridge.
6. Preserve reduced-motion, safe-area, overflow, and focus behavior already present in the token file.

## Acceptance checks

- All current `--hcs-*` consumers still resolve.
- No new page markup, JS, package, route, client, model, or auth change is needed.
- Representative existing dashboard, catalog, admin, document, workflow, survey, chat, and account pages inherit the updated shared tokens without horizontal page overflow.
- `git diff --name-only` contains only the three files in this phase plus plan/report documentation.

## Risks and mitigation

- A token change affects many pages: use compatibility aliases, inspect computed styles, and validate representative routes before proceeding.
- LeptonX/Bootstrap specificity can mask tokens: verify the existing `App.razor` load order and keep selectors scoped.
