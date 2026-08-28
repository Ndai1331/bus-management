# Document, calendar and signing UI improvements

Status: completed

## Overview

Improve three related Blazor UI areas from the supplied screenshots while preserving the existing HCS visual tokens and behavior:

1. Make the `/manage-documents` advanced filter readable and responsive.
2. Make calendar projects, tasks and events visually distinct.
3. Add a stable loading grid to `/document-signing`.

## Diagnosis

- `DocumentManagement.razor` renders eight advanced controls in `.hcs-catalog-advanced-filter`, whose shared CSS uses a single flex row and fixed maximum widths. This creates an oversized filter strip instead of a predictable responsive layout.
- `CalendarEventDisplay` assigns event items the cobalt `#3d5cff` accent, but `main.css` overrides `.hcs-cal-event--event` and its legend with the primary teal token. The rendered event therefore looks too close to the teal task style.
- `DocumentSigning.razor` loads data asynchronously but has no loading state or loading template around the grid, so the page has no grid-shaped feedback during the initial fetch or refresh.

## Implementation

- Add document-page-specific filter classes and a four-column desktop / one-column mobile layout, with consistent control sizing and a clear reset action.
- Use shared calendar accent constants in the calendar CSS so project, task and event colors match the data mapping and remain distinguishable.
- Add `isLoading`, render a row-based signing-grid skeleton during fetches, preserve existing data while refreshing, and keep empty state only for completed empty loads.

## Validation

- Build `src/HCS.Blazor/HCS.Blazor.csproj` without restore.
- Run `dotnet test HCS.slnx --no-build`.
- Run `git diff --check` and inspect the focused diff for existing dirty changes.

## Result

- Document filter uses a responsive grid: four columns by default, five on wide screens, nine on very wide screens, and one column on mobile.
- Calendar event styling uses cobalt `#3d5cff`, while tasks remain teal and projects remain orange.
- Document signing shows a seven-column skeleton grid during initial load and refresh, with a DataGrid loading template as a secondary fallback.
- Blazor build passed with 0 warnings and 0 errors.
- Solution tests passed.
- Targeted `git diff --check` passed for the changed UI files. Full-worktree whitespace output remains in the pre-existing `appsettings.Production.json` change.
- Visual browser verification was skipped because no Chrome MCP/devtools tool is available in this session.
