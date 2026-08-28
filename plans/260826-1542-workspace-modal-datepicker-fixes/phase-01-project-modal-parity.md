# Phase 01 — Project modal parity

## Context

- Canonical layout: `src/HCS.Blazor.Client/Pages/ProjectDetail.razor:12-104`.
- Current quick modal: `src/HCS.Blazor.Client/Pages/Workspace.razor:364-445`.
- Shared styles: `src/HCS.Blazor.Client/wwwroot/main.css:883-1067`, `1404-1420`, `2584-2603`.

## Requirements

- Keep `OpenProjectDetailModalAsync`, `CloseProjectDetailModalAsync`, `OpenTaskFromProjectModalAsync`, the project route target, `Work.GetProjectAsync`, and current notification/error behavior unchanged.
- Keep the quick modal read-only. Do not copy the route page’s save, add-member, remove-member, or full task-create behavior into this surface.
- Match the route page’s visual hierarchy: project name in the modal title when loaded, general-information section, members section, tasks section, `CardHeader` titles, consistent status/role labels, and the existing responsive two-column-to-one-column behavior.
- Preserve the current bounded task list and task-view callback; use existing row/action classes instead of introducing a second task component.
- Replace raw member-role output with the existing localized `ProjectRoleLabel` helper and existing member-list styling where practical. Keep the current member-name lookup/fallback.

## Implementation steps

1. Refactor only the project-detail modal markup in `Workspace.razor` to mirror the section order and card headers in `ProjectDetail.razor`.
2. Reuse `.hcs-detail-split`, `.hcs-catalog-form`, `.hcs-form-row--equal`, `.hcs-member-list`, and existing `WorkUi` badge helpers; keep values read-only rather than rendering editable inputs.
3. Keep the current “View detail” footer action as a normal project route link and preserve the modal-to-task transition. Ensure the modal closes before opening the existing task modal.
4. In `main.css`, remove or narrow obsolete custom grid styling only if the revised markup no longer references it; retain the bounded list and responsive rules needed by the quick surface.
5. Verify that no API/client/model or localization change is needed. If a value is not already in `ProjectDetailDto`, show the existing safe fallback rather than adding a new contract for visual parity.

## Success criteria

- The modal reads as the same project-detail surface as the canonical route, not as a separate summary-card design.
- Mobile modal content stacks without horizontal overflow; long project/member/task text remains truncated or wraps safely.
- All existing quick actions and route navigation still target the selected project/task ID.

## Risks

- Copying the full route editor would broaden scope and introduce duplicate mutation paths. Mitigate by preserving the quick modal’s read-only contract.
- Removing custom workspace rules too aggressively could affect the task-row action layout. Keep only the rules still referenced after the markup diff.

## Files

- Modify: `src/HCS.Blazor.Client/Pages/Workspace.razor`.
- Modify: `src/HCS.Blazor.Client/wwwroot/main.css` only for existing quick-surface selectors.
- Verify only: `src/HCS.Blazor.Client/Pages/ProjectDetail.razor`.
- Create/delete: none.
