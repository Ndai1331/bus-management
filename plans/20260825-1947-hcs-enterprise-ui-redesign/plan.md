---
title: "HCS enterprise UI redesign"
status: completed
progress: 100%
created: 2026-08-25
---

# HCS enterprise UI redesign

## Scope

Refresh the existing Blazor Web App shell and shared HCS visual primitives while preserving business logic, API calls, authentication, authorization, permissions, routes, DTOs, models, backend contracts, Bootstrap, LeptonX and Blazorise.

## Findings

- `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor` owns the current branded shell, manual permission-aware navigation, notification action, culture selector, user menu and BFF logout flow.
- The current shell is a two-row horizontal top navigation. It already has mobile accordion behavior, but desktop navigation is dense and does not provide a persistent page context.
- `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`, `main.css` and `hcs-components.css` provide shared tokens, catalog/table/form/modal styles and responsive utilities.
- `src/HCS.Blazor.Client/Pages` contains shared page families for workspace, catalogs, administration, documents, workflows, projects, surveys, chat and calendar. Most pages already use `.hcs-catalog-page`, `.hcs-document-page`, `.hcs-page__header`, Blazorise `Card`, `Button`, `DataGrid`, `Modal`, `Tabs`, `Select` and `Alert`.
- `src/HCS.Blazor/Components/App.razor` loads the LeptonX bundle, client CSS, Bootstrap/Blazorise assets and Font Awesome. It already has viewport metadata, Vietnamese font loading and a boot/error state.
- `HCSMenuContributor` remains the ABP navigation source, but the current shell renders its own permission-aware navigation. No package/vendor files will be edited.

## Design direction

Use a calm, dense enterprise workspace: deep navy navigation rail, teal primary action, slate text, white surfaces and a very light blue-gray canvas. Keep Be Vietnam Pro for Vietnamese readability. Use semantic tokens and restrained 150–220ms transitions; no gradients, emoji icons or decorative glass effects.

Core tokens: primary `#0F6D78`, primary strong `#0B5560`, primary soft `#E7F4F4`, ink `#1D2B3A`, muted `#64748B`, background `#F6F8FB`, surface `#FFFFFF`, border `#D8E1E8`, success `#147D64`, warning `#A35A00`, danger `#B42318`, info `#2563EB`.

## Implementation phases

1. Replace the horizontal shell presentation with a 248px desktop sidebar and 72px content header. Keep every existing `AuthorizeView`, route, nav target, notification callback, culture selector, user menu and logout method intact. Add desktop sidebar collapse and mobile off-canvas state with focus/escape/backdrop behavior.
2. Update HCS semantic tokens and Bootstrap-compatible variables. Keep LeptonX and Blazorise loaded; only override the application layer.
3. Refine shared page primitives: page headers, cards, buttons, inputs, selects, badges, alerts, tables/DataGrid containers, pagination, modal surfaces, focus rings and loading/empty/error states.
4. Apply responsive rules at 375/768/1024/1440px. Keep complex grids horizontally scrollable with a visible hint and preserve stacked table opt-in. Ensure chat keeps its full-height layout.
5. Validate compile/build and run the relevant test suite. Review the diff for contract/auth/route changes and check CSS for reduced-motion, keyboard focus, touch targets and horizontal overflow.

## Files to modify

- `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor`
- `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css`
- `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`
- `src/HCS.Blazor.Client/wwwroot/hcs-components.css`

## Validation

- `./scripts/audit-license-clean.sh`
- `dotnet build HCS.slnx --no-restore`
- `dotnet test HCS.slnx --no-build`
- Review rendered shell at 375px, 768px, 1024px and 1440px where the local host is available.

## Risks and mitigations

- Scoped CSS and global CSS load order may cause regressions: keep layout CSS scoped and place shared overrides in the already-loaded HCS component stylesheet.
- CSS-only visual changes can affect LeptonX/Blazorise generated markup: target stable Bootstrap/Blazorise classes and existing HCS wrappers, never vendor sources.
- The chat page relies on full-height overflow containment: preserve the existing `:has(.hcs-chat-page)` layout contract.
