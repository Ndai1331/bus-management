---
status: completed
progress: 100%
---

# Phase 3 — Shared styles and validation

## Objective

Align the remaining shared HCS component styles to the token foundation and prove that the bounded redesign did not change page behavior or application contracts. Do not migrate individual pages in this phase.

## Exact files

Modify:

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Components/GatewayDataPanel.razor.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Components/NotificationToast.razor.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Components/UserSignaturesPanel.razor.css`

The phase consumes the already modified token/shared files from Phases 1–2. No component Razor markup change is planned.

## Implementation steps

1. Replace local surface, border, spacing, status, and focus values in the three shared component styles with HCS semantic/component tokens.
2. Keep data-panel overflow containment, toast live-region behavior, dismiss/close affordances, signature-panel forms, and all existing state classes intact.
3. Check representative pages that consume these components: dashboard/workspace, document or workflow data surfaces, notifications, account/signature settings, catalog/admin lists, and mobile drawer states.
4. Run static audits, build/test checks, whitespace validation, and a changed-file boundary check.
5. Manually verify keyboard traversal, visible focus, reduced motion, long localized labels, loading/empty/error states, modal/drawer stacking, and narrow viewport containment.

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

If `HCS.sln` is not the solution path used by this checkout, use the solution path documented in `README.md` and record the exact command/result. The final changed-file list may include only the nine in-scope application files and the plan/report documentation.

## Acceptance checks

- Navigation and mobile audit scripts pass.
- Build and tests pass, or pre-existing environment/license failures are recorded separately with their exact output.
- No changes exist in `Pages/**`, typed clients, services, DTOs/models, auth/BFF, routes, module registration, backend, or JS files.
- No horizontal page scroll is introduced at 375px, 768px, 1024px, or 1440px.
- Keyboard focus remains visible around sticky shell, menus, drawer, modal, Select2, toast, and signature-panel controls.
- Reduced-motion mode disables non-essential transitions.

## Handoff

After review of the plan and its open decisions, implementation can start with:

`/ck:cook /Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/plans/260825-1950-enterprise-saas-ui-ux-redesign/plan.md`
