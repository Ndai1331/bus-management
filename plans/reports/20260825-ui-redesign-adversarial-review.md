# Adversarial UI Redesign Review

Review target: pending diff at `HEAD` (`codex/redesign-ui`). Application code was not modified by this review.

## Findings

### [High] Mobile drawer is covered by its own backdrop

`HCSMainLayout.razor.css:35-42` makes `.hcs-header` a `z-index: 100` stacking context. The drawer at `:96-113` is a descendant with `z-index: 250`, but the mobile backdrop at `:203` is a sibling with `z-index: 249`; the descendant cannot escape the header stacking context. At `HCSMainLayout.razor:225-227`, opening the drawer therefore paints the backdrop above the drawer and intercepts drawer clicks.

Action: put the drawer/backdrop in the same root stacking context, or lower the backdrop below the header context while keeping the drawer above it; verify click and keyboard activation at widths <=1100px.

### [High] Closed mobile drawer remains keyboard- and screen-reader reachable

The mobile rules at `HCSMainLayout.razor.css:200-203` only translate the closed nav off-screen. They do not set `visibility`, `pointer-events`, `inert`, or an equivalent accessibility state. The nav remains in the tab order and exposed to assistive technology while visually hidden.

Action: make the closed state non-interactive/non-visible and restore interaction only in `.hcs-top-nav--open`; test Tab traversal, Escape, and screen-reader navigation.

### [High] Collapsed desktop sidebar hides all grouped routes

`HCSMainLayout.razor.css:158-165` sets every `.hcs-nav-menu__panel` to `display: none` when collapsed. The corresponding trigger still exposes `aria-haspopup`/`aria-expanded` from `HCSMainLayout.razor:108-116` and `:154-165`, but clicking it cannot reveal its routes. Users must expand the rail before reaching every workflow, project, catalog, survey, and admin child route.

Action: provide a collapsed-state flyout/tooltip navigation, or keep submenu content reachable; keep `aria-expanded` synchronized with the rendered panel.

### [Medium] Chat loses its mobile full-height edge-to-edge layout

The chat override at `HCSMainLayout.razor.css:190-193` no longer resets padding. The <=1100px rule at `:204` changes only horizontal padding, leaving the generic top/bottom padding from `:190`. The previous mobile chat rule used `padding: 0`, so the chat surface now has an inset vertical gap and reduced usable height on mobile.

Action: add an explicit mobile chat rule that resets vertical padding and preserves the intended full-width/full-height contract; verify 375px and 768px with long message lists and the composer visible.

### [High] Global framework behavior is changed despite the no-framework-behavior guardrail

`hcs-tokens.css:21-41` globally redefines Bootstrap variables (`--bs-primary`, body colors, borders, links, and radii) and `:2` forces `color-scheme: light`. These styles load after the LeptonX bundle in `src/HCS.Blazor/Components/App.razor:22-28`. In addition, `hcs-components.css:628-676` applies broad `.hcs-app-shell .btn`, form, table, card, modal, and alert rules; the specificity and load order override Bootstrap/Blazorise defaults inside the entire application shell. This is a direct change to Bootstrap/Blazorise/LeptonX behavior, not an isolated HCS visual wrapper.

Action: remove the global framework bridges or scope them to explicitly approved HCS surfaces/variants, then compare computed styles for LeptonX, Blazorise DataGrid, Modal, Button, Select, and small/large Bootstrap buttons.

### [Medium] New button selector overrides size variants and local component styles

The added `.hcs-app-shell .btn` rule at `hcs-components.css:628-634` has higher specificity than `.btn-sm`/`.btn-lg` and is loaded after client scoped styles (`App.razor:24-28`). It forces `min-height`, padding, font size, and weight on existing small actions such as ChatWorkspace retry/save/member buttons and notification actions, creating layout and density regressions.

Action: use lower-specificity token defaults or preserve `.btn-sm`/`.btn-lg`/component-specific selectors explicitly; validate chat, toast, catalog, and modal actions at narrow widths.

### [Medium] Signature primary-button focus indicator is removed

The existing focus rule at `UserSignaturesPanel.razor.css:106-114` supplies an outline. The added rule at `:494-495` matches the same scoped selectors and sets `outline: 0` for `.hcs-signature-button--primary:focus-visible` and the upload CTA focus state. The background change is not a reliable focus indicator.

Action: retain a visible token-based outline or focus ring for both keyboard states; verify keyboard traversal in signature settings.

### [Medium] Navigation audit was weakened and now misses the new failure modes

`scripts/audit-navigation-layout.sh:8-17` deletes the previous desktop alignment assertions and replaces them with string-presence checks for the sidebar. The passing result cannot detect the drawer stacking, closed-state focus exposure, collapsed submenu reachability, or CSS specificity regressions above.

Action: retain the old alignment assertions where still applicable and add executable checks for drawer visibility/interactivity and collapsed navigation reachability instead of changing the oracle to match the implementation.

### [Low] New collapse control is not localized

`HCSMainLayout.razor:34-40` adds hard-coded Vietnamese `aria-label` and `title` text. English users receive Vietnamese accessibility names, violating the plan's localization guardrail.

Action: use localized resource keys for both labels, with distinct expand/collapse strings.

### [Medium] Persistent rail is inconsistent with the active redesign plan

The shell rewrite at `HCSMainLayout.razor.css:96-113` replaces the two-row desktop navigation with a persistent rail. The active phase plan explicitly requires the two-row desktop pattern and says a persistent rail requires separate approval (`plans/260825-1950-enterprise-saas-ui-ux-redesign/phase-02-shared-surfaces.md:5,26`). A conflicting older untracked plan proposes the rail, so approval is ambiguous rather than proven.

Action: obtain an explicit rail approval and update the active plan, or restore the planned two-row desktop information architecture before landing.

## Boundary verification

No added diff lines changed business logic, API calls, typed clients, authentication/authorization policies, permissions, route templates/targets, DTOs/models, backend contracts, JS, package references, or module registration. This is a static diff finding; it does not waive the framework-CSS behavior findings above.

## Verification evidence

- `./scripts/audit-license-clean.sh`: passed.
- `./scripts/audit-navigation-layout.sh`: passed (but is weakened as noted above).
- `./scripts/audit-mobile-layout.sh`: passed.
- `dotnet build HCS.slnx --no-restore`: passed, 0 warnings, 0 errors.
- `git diff HEAD --check`: passed.

Status: DONE_WITH_CONCERNS
