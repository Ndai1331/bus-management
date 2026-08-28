---
title: "HCS top-menu and color-system scan"
description: "Concise scan of the current HCS shell, styles, token consumers, and navigation audit before the follow-up implementation."
status: completed
created: 2026-08-26
tags: [frontend, ui, accessibility, scan]
---

# HCS top-menu and color-system scan

## Summary

The requested follow-up is feasible without touching business or framework contracts. The current worktree’s `hcs-top-nav` is visually a fixed desktop sidebar: it uses sidebar width variables, fixed positioning, and header/content left margins. The same markup already provides a mobile drawer at `max-width: 1100px`; that behavior should be retained while the desktop CSS is converted to a horizontal header menu.

No application code was modified during this scan.

## Findings

| File | Current finding | Plan implication |
|---|---|---|
| `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor` | Owns all primary links, nested menu render fragment, `AuthorizeView` guards, active-route logic, notification/chat/account actions, BFF logout, mobile toggle, Escape handling, and focus return. It also owns `sidebarCollapsed` and the collapse button. | Remove only sidebar-specific state/control if needed; preserve the navigation/authorization tree and callbacks verbatim. |
| `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css` | `.hcs-top-nav` is fixed from `top: 0` to `bottom: 0` with a sidebar width; `.hcs-header__top` and `.hcs-main-content` use the same left margin; collapsed-sidebar flyout rules remain. The `<=1100px` drawer already has transform, backdrop, visibility, and safe-area rules. | Restore static/relative horizontal desktop layout; retain and test the existing mobile branch. |
| `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css` | Canonical tokens are currently `--hcs-color-*` with the prior teal/navy/blue-gray values, plus spacing, surface, shadow, z-index, motion, and focus tokens. | Add exactly the ten requested `--color-*` tokens and map existing `--hcs-*` names to them for compatibility. |
| `src/HCS.Blazor.Client/wwwroot/hcs-components.css` | Shared primitives already use HCS aliases for most surfaces, focus, tables, forms, data grids, chat, modals, and responsive behavior, but some fallback/alpha literals remain. | Normalize dominant palette use through aliases without changing component contracts or table/chat containment. |
| `src/HCS.Blazor.Client/wwwroot/main.css` | Contains global/body/Bootstrap/LeptonX bridges, boot/error styles, control sizing, and many legacy color literals. | Keep asset/framework integration intact; update application-layer variable bridges and dominant literals only. |
| `scripts/audit-navigation-layout.sh` | Positive assertions currently require the fixed sidebar, left margins, sidebar width, and collapsed-sidebar flyouts, plus mobile markup isolation. | Change assertions to verify horizontal desktop menu structure and the `<=1100px` drawer contract. |

## Navigation contract inventory

The shell currently exposes workspace, documents, workflows, projects/tasks, calendar, surveys, catalogs, administration, and permission-gated chat. Existing guards include `Documents.View`, `Documents.Assign`, `Documents.Signing.Execute`, `HCS.Organization.MasterData`, `HCS.Organization.Departments`, `HCS.Organization.Units`, `HCS.Organization.Positions`, `Roles="admin"`, and `Collaboration.Chat`. Document query strings such as `sourceType=0/1/2` and the legacy active-route handling must remain unchanged.

The shell also owns `CultureSelector`, `NotificationToast`, unread counts, avatar loading/fallback, account navigation, and BFF logout URL construction. These are out of scope for visual restructuring.

## Exact palette decision

The new canonical dominant layer is exactly:

```text
--color-primary       #00B4A9
--color-secondary     #007F7C
--color-primary-dark  #007F7C
--color-primary-light #E0F7F5
--color-teal-top      #007F7C
--color-accent        #E31E24
--color-text          #1A1A2E
--color-muted         #5C6578
--color-border        #DDE4EE
--color-bg            #F5F8FC
```

Existing `--hcs-color-*` names remain aliases so page and component CSS do not break. White remains the existing neutral surface; status aliases are compatibility/status-only mappings, not additional dominant `--color-*` tokens.

## Review and validation

The implementation plan at [`../20260826-top-menu-color-system/plan.md`](../20260826-top-menu-color-system/plan.md) was reviewed against the README, workspace architecture/design guidance, current target files, existing completed UI plans, and the dirty-worktree boundary. It is limited to six files, has no cross-plan blocker, makes the explicit `1100px` breakpoint authoritative, and includes contract, accessibility, overflow, token, and CSS-specificity safeguards.

Planned validation: `./scripts/audit-license-clean.sh`, `./scripts/audit-navigation-layout.sh`, `dotnet build HCS.slnx --no-restore`, `dotnet test HCS.slnx --no-build`, `git diff --check`, and manual checks at `1440px`, `1101px`, `1100px`, `768px`, and `375px`.

## Unresolved questions

None blocking. Implementation should keep white as the neutral surface and preserve status text/icon semantics while using only the ten requested dominant palette tokens.
