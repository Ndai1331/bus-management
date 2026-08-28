# Workspace quick project-detail modal review

Reviewed the current working-tree versions of:

- `src/HCS.Blazor.Client/Pages/Workspace.razor`
- `src/HCS.Blazor.Client/Pages/ProjectDetail.razor`
- `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`
- `src/HCS.Blazor.Client/wwwroot/hcs-components.css`
- `src/HCS.Blazor.Client/wwwroot/main.css`

No application source files were changed by this review. `Workspace.razor` and `main.css` already contained uncommitted quick-view changes; this report evaluates their current content.

## Recommendation

The current modal is structurally close to the route page: `hcs-detail-split` now places General information and Members together, Tasks follows below, and the cards use the same `CardHeader` pattern. Keep that structure. The smallest safe follow-up is:

1. Make the modal title show the project name after loading, with the existing generic title as fallback. This matches `ProjectDetail.razor:15` and gives the dialog a useful accessible name (`Workspace.razor:367`).
2. Render “View detail” as an anchor to `/project-detail/{id}` instead of a button that calls `Navigation.NavigateTo` (`Workspace.razor:441`, `Workspace.razor:559-563`). This matches the route page’s navigation pattern and preserves normal link behavior such as open-in-new-tab.
3. Mark the loading block as `role="status" aria-live="polite"` and hide the spinner icon from assistive technology (`Workspace.razor:373`).
4. Keep new quick-view CSS token-driven. Replace the new `#24465e` description color (`main.css:2608-2611`) with `var(--hcs-color-ink)`; do not introduce another modal palette. Existing `hcs-catalog-desc` and member-list styles are shared with the route and can remain unchanged in this small pass.

## Accessibility and interaction checks

- The task eye controls already use real buttons and localized `aria-label`s. Their shared rule (`main.css:2561-2591`) currently sets `width: 1.85rem`, uses undefined legacy `--hcs-primary*` fallbacks, and suppresses the global focus outline with `outline: none`. Make the control at least `var(--hcs-touch-target)` square, use `--hcs-color-primary`/`--hcs-color-primary-soft`, and replace the outline suppression with `box-shadow: var(--hcs-focus-ring)` (or another equally visible tokenized focus treatment).
- Verify focus moves into the modal, stays trapped while open, returns to the originating eye button after close, and transfers cleanly when a task opens the next modal (`Workspace.razor:429`, `Workspace.razor:565-569`). Verify Escape and the close button both work.
- Keep the visible status text beside each task badge; status is not communicated by color alone. Add `aria-hidden="true"` to the decorative external-link icon in the footer if it remains.
- If the modal library does not already provide the relationship, ensure `ModalTitle` supplies the dialog’s accessible label; the dynamic title recommendation makes that label project-specific.

## Responsive checks

- At 320px and 375px: confirm the modal title, footer actions, status badges, and long project/member names wrap without horizontal overflow. The current overflow rule covers the project name and description but not the code value or member names; extend it to all read-only values if needed.
- At 768–991px: confirm the route-consistent `hcs-detail-split` remains one column, as defined by `main.css:1411-1415`; at 992px and above, confirm the two-column General/Members split has no cramped member list.
- With many tasks: confirm the nested task list (`main.css:2617`) scrolls without the modal/page scroll fighting it. Add `overscroll-behavior: contain` to the modal body or nested list; the shared modal body currently only sets `overflow-y: auto` (`hcs-components.css:413-416`).
- Test long translated labels and long unbroken identifiers at the smallest supported width; keep `min-width: 0` on the task title and preserve the existing `text-truncate` behavior.

## No-change conclusion

Do not duplicate the route’s editable `ProjectFields` in the quick view. The current read-only summary is the right quick-view scope; align its title, navigation semantics, token usage, and keyboard/mobile behavior only.

