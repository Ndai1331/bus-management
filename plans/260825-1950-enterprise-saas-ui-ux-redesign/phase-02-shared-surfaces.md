---
status: completed
progress: 100%
---

# Phase 2 — HCS shell and navigation styling

## Objective

Refine the existing HCS shell over LeptonX into a predictable enterprise workspace while preserving its current navigation model and behavior. The default direction is an improved two-row top navigation with a consistent mobile drawer; a persistent rail requires separate approval and is not part of this plan.

## Exact files

Modify:

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css`
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Layouts/CultureSelector.razor.css`

Verify only:

- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Routes.razor` — preserve `BffAuthenticationGate`, `AuthorizeRouteView`, not-authorized output, and layout selection.
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Navigation/HCSMenuContributor.cs` — preserve ABP menu URLs, permissions, and labels; keep metadata aligned if the shell's visual grouping is clarified.
- `/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor/HCSBlazorModule.cs` — preserve LeptonX `side-menu` and Blazorise bundle setup.

## Implementation steps

1. Preserve every existing navigation destination, compatibility alias, `AuthorizeView` condition, notification/chat/account action, culture action, logout action, and mobile drawer interaction.
2. Make only presentation/semantic improvements to the shell markup: clear landmarks, stable heading/skip-target relationships, named icon actions, active-route presentation, and accessible menu/drawer state attributes where existing behavior supports them.
3. Use the token layer for header heights, gutters, surfaces, borders, focus rings, dropdown/drawer layers, and text/status contrast.
4. Keep the two-row desktop layout, with predictable grouping and active state; keep the current mobile breakpoint/drawer concept and make it keyboard-operable without hover-only access.
5. Add or preserve `scroll-padding-top`, sticky-header offsets, focus-visible styles, safe-area padding, and z-index ordering so deep-link focus is not hidden behind the shell.
6. Keep culture switching, BFF navigation, `data-enhance-nav="false"`, and all existing JS initialization unchanged.

## Acceptance checks

- All visible shell links and permission gates remain present and point to the same routes.
- Desktop, tablet, and mobile navigation are usable with keyboard and do not depend on hover.
- Notification, chat unread, culture, account, logout, and unauthorized states remain behaviorally unchanged.
- `scripts/audit-navigation-layout.sh` and `scripts/audit-mobile-layout.sh` pass.
- `git diff --name-only` contains only the three shell files in this phase plus plan/report documentation.

## Risks and mitigation

- Sticky header, drawer, dropdown, modal, Select2, and toast layers may compete: use the existing z-index token scale and test direct deep links.
- Shell markup is coupled to localization and authorization: preserve existing keys and `AuthorizeView` boundaries; do not introduce new copy or permission logic.
- LeptonX may provide hidden responsive rules: inspect the rendered DOM at 375px, 768px, 1024px, and 1440px before finalizing overrides.
