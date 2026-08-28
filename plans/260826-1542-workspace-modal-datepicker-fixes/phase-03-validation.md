# Phase 03 — Validation and handoff

## Checks

1. Record the current dirty-worktree baseline and review the existing diff in `Workspace.razor` and `wwwroot/main.css` first. The shared worktree already contains edits matching this plan; preserve them and validate/reconcile instead of reapplying or resetting them.
2. Run the repository license audit, focused Blazor host build, solution tests, and `git diff --check` from the work context.
3. Exercise `/workspace` at 1440px, 992px, 768px, and 375px. Open the quick project modal and date picker at each relevant breakpoint.
4. Verify keyboard focus, Escape/close, click-through on calendar days, modal footer actions, empty lists, long localized labels, and no page-level horizontal overflow.
5. Record any environment-only failure separately; do not change dependencies or runtime secrets to make a UI check pass.

## Definition of done

- All commands in `plan.md` pass, or failures are explicitly classified as pre-existing/environmental.
- Manual checks confirm modal parity and date-popup layering without behavioral regressions.
- `git diff --name-only` shows the two planned application files plus intentional plan/report artifacts and any pre-existing user changes; no unrelated file is newly touched by this task.
