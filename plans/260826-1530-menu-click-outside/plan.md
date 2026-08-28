# Close open navigation menus on outside click

Status: completed

## Diagnosis

`HCSMainLayout.razor` renders `.hcs-nav-backdrop` only when `mobileNavOpen` is true. Desktop dropdowns are controlled by `expandedSection`, so an open menu has no outside-click target and remains open until another menu is hovered or toggled.

## Fix

- Render the existing backdrop whenever the mobile drawer, navigation dropdown or account menu is open.
- Add desktop backdrop styling below the header and below the dropdown z-index.
- Reuse `CloseMobileNavAndFocus` so the existing Escape/focus behavior remains consistent.

## Validation

- Build `src/HCS.Blazor/HCS.Blazor.csproj` without restore.
- Run the existing solution tests.
- Review the diff and verify that clicks inside menu panels are not intercepted.

## Result

- Build passed with 0 warnings and 0 errors.
- Solution tests passed.
- `git diff --check` passed.
- The transparent outside-click layer is below the header/menu panels, so menu content remains clickable.
