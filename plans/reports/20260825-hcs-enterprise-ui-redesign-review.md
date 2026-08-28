# HCS enterprise UI redesign review

## Actionable findings

- **P1 — Mobile drawer focus isolation:** `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css:195-205` only translates the closed drawer off-screen; its links remain tabbable. When open, there is no focus trap or inert page background. Add closed-state `visibility`/`pointer-events` or `inert`, contain focus while open, and preserve focus return to the toggle.
- **P1 — Collapsed rail hides navigation:** `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css:151-165` clips submenu panels and hides labels in collapsed mode, with no tooltip/flyout/accessibility alternative. Provide a reachable child-menu interaction or keep labels available.
- **P1 — Localization regression:** `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor:38-39` hard-codes Vietnamese collapse/expand labels and titles. Move them to localized resources so English users receive correct accessible names.
- **P1 — Shared styling is overridden:** `src/HCS.Blazor.Client/wwwroot/hcs-components.css:607-675` uses lower-specificity `:where()` rules than existing catalog DataGrid, modal, and form rules in `src/HCS.Blazor.Client/wwwroot/main.css:704-736,857-883,1038-1108`. Raise/reshape the shared selectors or remove the competing rules so the tokenized styling actually applies consistently.
- **P2 — Sticky-shell focus/scroll behavior:** `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css:35-51,190-205` lacks `scroll-padding-top`/focus offsets and drawer `overscroll-behavior: contain`. Add both to prevent focused content being covered and scroll chaining through the drawer.
- **P2 — Rail label contrast:** `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css:156` renders section labels at approximately 3.53:1 against the navy rail. Increase contrast to meet the small-text target.
- **P2 — Gradients remain against the direction:** `src/HCS.Blazor.Client/Components/GatewayDataPanel.razor.css:2,17`, `src/HCS.Blazor.Client/wwwroot/hcs-components.css:340`, and `src/HCS.Blazor.Client/Components/UserSignaturesPanel.razor.css:368` still use gradients. Replace decorative gradients; use a non-gradient skeleton treatment if the “no gradients” rule is strict.
- **P2 — Reduced-motion gap:** `src/HCS.Blazor.Client/wwwroot/main.css:45-53,91-95` leaves the boot spinner animated under `prefers-reduced-motion`. Disable that animation as well.
- **P2 — Design-system source conflict:** `design-system/hcs-enterprise-workspace/MASTER.md` specifies Inter/`#2563EB`/orange CTA, while `src/HCS.Blazor.Client/wwwroot/hcs-tokens.css:4-43` implements Be Vietnam Pro/teal. Choose one approved source of truth before visual sign-off.

Status: DONE_WITH_CONCERNS
