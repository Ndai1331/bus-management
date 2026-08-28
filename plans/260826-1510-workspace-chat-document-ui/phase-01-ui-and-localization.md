# Phase 01 — UI and localization

Status: completed

## Related files

- `src/HCS.Blazor.Client/Pages/Workspace.razor`
- `src/HCS.Blazor.Client/Pages/ChatWorkspace.razor`
- `src/HCS.Blazor.Client/Pages/ChatWorkspace.razor.css`
- `src/HCS.Blazor.Client/Pages/SurveySessions.razor`
- `src/HCS.Blazor.Client/Pages/DocumentManagement.razor`
- `src/HCS.Blazor.Client/Pages/DocumentDetail.razor`
- `src/HCS.Blazor.Client/Documents/DocumentModels.cs`
- `src/HCS.Domain.Shared/Localization/HCS/vi.json`
- `src/HCS.Domain.Shared/Localization/HCS/en.json`
- `src/HCS.Blazor.Client/wwwroot/main.css`

## Steps

1. Add document-status localization/key/class helpers beside the existing document contracts.
2. Update workspace chart markup and localized document status output.
3. Update chat contacts markup/CSS so contacts scroll independently from the bottom CTA.
4. Replace raw survey and document status/action labels with localized resources.
5. Add the localized status badge to document detail.
6. Validate syntax, JSON and build output.

## Risks and mitigations

- Donut rendering must handle zero workflows: use a neutral empty state rather than an invalid gradient.
- Status values sent to APIs remain the original English enum/string values; only display text changes.
- The contacts CTA must remain usable on short viewports, so the contact list gets `min-height: 0` and `overflow: auto`.
