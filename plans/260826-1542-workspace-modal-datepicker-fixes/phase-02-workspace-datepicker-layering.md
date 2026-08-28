# Phase 02 — Workspace date-picker layering

## Context

- Range markup: `src/HCS.Blazor.Client/Pages/Workspace.razor:12-21`.
- Wrapper: `src/HCS.Blazor.Client/Components/HcsDatePicker.razor:4-18`.
- Workspace filter/KPI layout: `src/HCS.Blazor.Client/wwwroot/main.css:2011-2133`.
- Existing z-index tokens: `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css:70-77`; `--hcs-z-popover` is the intended popover layer.

## Likely cause

The range picker lives inside the first workspace filter, while the KPI/summary cards are later siblings. The filter/date wrapper has no application-owned stacking context or popup layer, so the rendered Blazorise calendar can fall behind later card surfaces when the vendor popup z-index is absent, overridden, or constrained by the active CSS bundle/order. Do not change vendor CSS to solve a page-local layering problem.

## Implementation steps

1. Confirm the existing Blazorise `.datepicker-calendar.dropdown-menu` selector and the existing z-index tokens from the source/CSS bundle.
2. Add narrowly scoped rules in `wwwroot/main.css` for the Workspace date wrapper/calendar, using existing `--hcs-z-sticky` and `--hcs-z-popover` tokens and no new token:

   ```css
   .hcs-workspace .hcs-ws-filter-dates {
       position: relative;
       z-index: var(--hcs-z-sticky);
   }

   .hcs-workspace .hcs-ws-filter-dates .datepicker-calendar.dropdown-menu {
       z-index: var(--hcs-z-popover);
   }
   ```

   Adjust only the selector/class evidence if the runtime markup differs. Add `overflow: visible` only if browser inspection proves an ancestor is clipping the popup.
3. Keep the rule page-local; do not alter global `.dropdown-menu`, Bootstrap/Blazorise assets, `hcs-tokens.css`, `HcsDatePicker.razor`, or other date-picker consumers.
4. Verify from source/build that the popup remains above cards while the page wrapper stays below drawer/modal/notification layers; authenticated browser verification remains manual because no browser harness is available.

## Success criteria

- The complete range calendar is painted above KPI cards and accepts pointer/keyboard input.
- Existing range selection, `OnRangeChanged`, data loading, display format, and Search behavior are unchanged.
- The fix does not change date pickers in project/task forms or catalog pages.

## Files

- Modify: `src/HCS.Blazor.Client/wwwroot/main.css`.
- Verify only: `src/HCS.Blazor.Client/Pages/Workspace.razor`, `Components/HcsDatePicker.razor`, `wwwroot/hcs-tokens.css`.
- Create/delete: none.
