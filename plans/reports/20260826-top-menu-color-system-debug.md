# HCS Top Menu / Color System Regression Diagnosis

## Status

**DONE — confirmed risks remain.** Read-only source diagnosis completed on 2026-08-26. No application files were modified.

## Scope and evidence

Inspected:

- `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor`
- `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css`
- `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`
- `src/HCS.Blazor.Client/wwwroot/hcs-components.css`
- `src/HCS.Blazor.Client/wwwroot/main.css`
- `scripts/audit-navigation-layout.sh`
- `scripts/audit-mobile-layout.sh`
- Chat layout CSS and stylesheet load order as dependent evidence.

Executed successfully:

- `./scripts/audit-navigation-layout.sh` → `Desktop navigation alignment audit passed.`
- `./scripts/audit-mobile-layout.sh` → `Mobile layout containment audit passed.`
- `git diff --check` on the inspected change set → no output/errors.

The local HTTPS host at `https://localhost:44403` was unavailable (`HTTP 000`), so this report is based on source/cascade analysis; no runtime screenshot claim is made.

## Confirmed risks

### R1 — P1: 1101px is an abrupt desktop/wrapping boundary

At exactly `1100px`, `@media (max-width: 1100px)` activates the fixed drawer. At `1101px`, that media query no longer applies and `.hcs-top-nav` becomes the desktop `display:flex; flex-wrap:wrap` row. The desktop compact rule previously covering the 1100–1280 range is gone. Long Vietnamese labels can therefore produce a two-line desktop menu immediately above the drawer breakpoint, with a visibly discontinuous layout at 1100/1101px.

Evidence: `HCSMainLayout.razor.css:86-99`, `168-188`; previous compact-desktop rules are removed in the current diff.

### R2 — P1: mobile header box is 64px while its top row remains 72px

The mobile media query sets `.hcs-app-shell`/`.hcs-header` to `64px` (`:169-170`), but `.hcs-header__top` retains the base `height: var(--hcs-header-height)` declaration and is not separately reduced (`:39-45`). Its computed height is therefore 72px while the header layout box is 64px. The fixed drawer and backdrop start at `top: var(--hcs-header-height)` = 64px (`:173`, `:182`), overlapping the last 8px of the top-row box when open.

Affected widths: exactly `1100px`, `768px`, and `375px`.

### R3 — P1: 375px header actions exceed the available row width when chat permission is present

At `375px`, the brand remains non-shrinking, the hamburger is a fixed 44px control, and the actions remain non-shrinking. The actions contain the language selector, notification button, permission-gated chat shortcut, and user summary. The language selector plus three 44px/action controls, gaps, brand, hamburger, and horizontal padding require more than the 348px content width left by the 0.85rem side padding. There is no shrink/wrap rule for these flex items. `body { overflow-x: hidden; }` hides the resulting overflow instead of making the clipped action reachable.

Evidence: markup `HCSMainLayout.razor:18-88`; fixed/non-shrinking controls `HCSMainLayout.razor.css:39-45`, `63-80`; mobile rules `:168-193`; global clipping `hcs-tokens.css:88-90`.

### R4 — P1: the mobile catalog accordion can clip its last entries

Mobile submenu panels use `max-height: 42rem` and `overflow: hidden` (`HCSMainLayout.razor.css:177`). The catalog panel contains 15 possible links plus four section labels (`HCSMainLayout.razor:155-191`). Global component CSS raises the link minimum to 44px (`hcs-components.css:179-185`), so the links alone consume 660px; labels, gaps, and padding make the full panel taller than 42rem. Because the panel itself is `overflow:hidden`, the outer drawer cannot scroll the clipped panel content. This affects a fully authorized catalog menu at `375px` and `768px`.

### R5 — P1: keyboard focus can keep a desktop submenu or account panel visually open after state close

Both nav panels and the user panel are made visible by `:focus-within` in addition to their component state (`HCSMainLayout.razor.css:134-154`). The nav trigger prevents default mousedown focus (`HCSMainLayout.razor:100-103`, `146-151`, `319-324`). For keyboard activation, the trigger remains focused; `ToggleSection`/`CloseMobileNav` clears the state, but `:focus-within` still matches, so a desktop submenu can remain visible after its state is closed. The same pattern applies to the account panel when Escape sets `userMenuOpen = false` while focus remains in the `<details>` (`HCSMainLayout.razor:54-85`, `395-399`).

### R6 — P1: inert notification content can still paint above the drawer

When the drawer opens, `.hcs-app-shell__notifications` receives `inert` and `aria-hidden` (`HCSMainLayout.razor:220-229`), which removes interaction/focus but does not hide painted content. Notification toasts/panel retain fixed `z-index: 1080/1075` (`Components/NotificationToast.razor.css:1-10`, `84-94`), while the drawer is inside the header stacking context (`z-index:100`) and the backdrop is `99` (`HCSMainLayout.razor.css:31-37`, `182`). An already-open notification panel or toast therefore remains visually above the mobile drawer while being inert/unusable.

### R7 — P1: requested primary token is used with insufficient white-text contrast

The canonical `--color-primary: #00B4A9` is correctly declared and aliased (`hcs-tokens.css:2-29`), but it is used as a solid background with `#fff` in primary buttons and related controls (`hcs-components.css:664-670`, `main.css:1801-1805`, `1938-1949`, `2044-2050`). The calculated contrast ratio is approximately **2.59:1**, below 4.5:1 for normal-size text. The darker `#007F7C` token is approximately 4.85:1 against white; the issue is specifically the lighter primary background pairing.

### R8 — P1: color migration is incomplete across the inspected surface

The token file contains the requested canonical palette, but dominant legacy colors remain in `main.css` (`#19334c` at `:973`/`:1518`, `#355dff` at `:1325`, `#2f4ae6` at `:1952`/`:2058`, and `#2563eb` at `:2206`). The dependent chat scoped CSS also retains an independent blue palette, beginning with `--chat-primary: #3d5cff` (`Pages/ChatWorkspace.razor.css:1-14`). The resulting workspace/catalog/chat/status surfaces cannot be considered consistently driven by the requested color system.

### R9 — P2: stylesheet order intentionally overrides the chat height calculation

`App.razor` loads `main.css` before `hcs-components.css` (`Components/App.razor:24-30`). `main.css` sets chat main height to `calc(100dvh - var(--hcs-header-h))` (`main.css:385-393`), while the later global rule sets the same selector’s height to `auto` (`hcs-components.css:719-728`). The flex chain still supplies height through `.hcs-app-shell:has(.hcs-chat-page)` and `.hcs-main-content:has(.hcs-chat-page)` (`HCSMainLayout.razor.css:163-166`), so the normal chat path is structurally full-height, but the effective rule is load-order dependent and the explicit height calculation is not the applied value.

## Breakpoint result matrix

| Viewport width | Effective mode | Confirmed result |
|---|---|---|
| `>1100px` | Desktop top nav | Normal second row; `flex-wrap: wrap`; dropdowns are not clipped by `.hcs-top-nav` because it has `overflow: visible`. Near 1101px, wrapping is confirmed by the CSS contract. |
| `1100px` | Mobile drawer | Drawer/backdrop/focus-isolation rules apply. Header is 64px but top row remains 72px (R2). |
| `768px` | Mobile drawer + tablet page rules | Drawer applies; chat switches to single-panel layout at its `max-width:860px` rule. The `max-width:767.98px` page-stack rules do not apply. Header overlap remains (R2); catalog panel clipping remains (R4). |
| `375px` | Mobile drawer + phone page rules | Drawer width resolves to 320px (`min(320px, 86vw)`). Header action row can overflow/clip when chat shortcut is authorized (R3); header overlap and catalog clipping remain (R2/R4). |

## DONE: preserved contracts

- `HCSMainLayout.razor` retains the existing destination markup, including workspace, documents/query-string variants, workflows, projects/tasks, calendar, surveys, catalogs, reports, administration, and chat.
- Existing permission wrappers remain present: `Documents.View`, `Documents.Assign`, `Documents.Signing.Execute`, `HCS.Organization.MasterData`, `HCS.Organization.Departments`, `HCS.Organization.Units`, `HCS.Organization.Positions`, `Roles="admin"`, and `Collaboration.Chat`.
- Existing `NavLinkMatch="NavLinkMatch.All"`, query strings, `Navigation.LocationChanged`, avatar callbacks/fallback, notification/chat unread callbacks, `NotificationToast`, `CultureSelector`, account menu, BFF logout construction, and `forceLoad: true` remain in place.
- Mobile close/reset behavior, conditional backdrop, `inert`/`aria-hidden` attributes, Escape handler, route-change reset, and focus return to `navToggle` are present in source. The risks above are interaction/cascade limitations around those preserved mechanisms, not missing markup.
- Both repository navigation audits pass; they are positive string-presence checks and do not detect R1–R9.

## Final disposition

**DONE with confirmed risks R1–R9.** No application code, CSS, markup, route, permission, or audit script was changed by this diagnosis.
