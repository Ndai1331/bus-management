# Document and signing column spacing

Status: completed

## Overview

Add explicit responsive gutters to the two-column document detail form and approval/signing modal so the 50/50 columns have clear visual separation without creating overflow on smaller screens.

## Diagnosis

- `DocumentDetail.razor` uses an unclassified `Row` containing two `Column` components with `Is12.OnMobile.Is6.OnDesktop`.
- `DocumentSigning.razor` uses the same two-column pattern inside `.signing-action-modal`.
- Neither row has a component-specific gutter token/class, so the visible separation relies on the current framework gutter defaults and appears collapsed in the supplied screenshots.

## Implementation

- Add semantic classes to the document detail row and signing modal row.
- Set a deliberate desktop `--bs-gutter-x` and vertical gutter for both rows.
- Remove horizontal gutter on stacked mobile columns while retaining vertical spacing.

## Validation

- Build `src/HCS.Blazor/HCS.Blazor.csproj` without restore.
- Run `dotnet test HCS.slnx --no-build`.
- Run targeted `git diff --check` and inspect the focused diff.

## Result

- `document-detail` now uses an explicit 2rem horizontal gutter between the form and preview columns.
- The approval/signing modal uses the same separation, with a 1.5rem vertical gutter for stacked content.
- Mobile removes horizontal gutter and preserves vertical spacing.
- Build passed with 0 warnings and 0 errors.
- Solution tests passed.
- Targeted `git diff --check` passed.
