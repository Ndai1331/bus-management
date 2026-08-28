# Final code review — HCS enterprise UI redesign

## Actionable findings

- **P1 — Collapsed sidebar flyouts remain clipped.** `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor.css:97-115` sets the sidebar to `overflow-x: hidden`, while the collapsed flyout at `:167-181` is absolutely positioned beyond the 76px rail. Expanding a grouped item therefore cannot expose reachable child links. Render the flyout outside the clipping scroll container or use a non-clipped positioned layer.
- **P1 — Mobile drawer focus is not isolated or reliably returned.** The closed drawer now has `visibility: hidden`/`pointer-events: none` and the stacking order is correct, but an open drawer has no focus trap/inert background. Activating a drawer link calls `CloseMobileNav` without restoring focus to `navToggle`, so focus can escape to the backdrop/page or remain in the hidden drawer. Trap focus (or inert/aria-hide the background) and restore focus on every close path, including route activation.
- **P2 — Chat’s mobile full-height reset is overridden.** `HCSMainLayout.razor.css:224` uses the lower-specificity `.hcs-main-content:has(.hcs-chat-page)` rule, while `wwwroot/main.css:385-393` retains the higher-specificity `.hcs-app-shell .hcs-main-content:has(.hcs-chat-page)` with `padding: 1.5rem ...`; `App.razor:24-28` loads the scoped bundle before `main.css`. At mobile widths the new `padding: 0` does not win, leaving the chat inset and reducing usable height. Consolidate the rule or add an equal/higher-specificity later override.
- **P2 — Focus-ring tokens are inconsistent.** `wwwroot/hcs-tokens.css:69` defines a teal ring, but `wwwroot/hcs-components.css:2-4,32-36` overrides the shell ring and outline with the old blue values; newer culture/signature rules use teal. Make the ring fully token-driven and remove the duplicate hard-coded blue focus styling.
- **P2 — The no-gradient direction is incomplete.** The changed background gradients were removed, but gradients remain in `wwwroot/hcs-components.css:340`, `Components/GatewayDataPanel.razor.css:17`, and `Components/UserSignaturesPanel.razor.css:368`. Replace the decorative fade and use a solid/opacity-based loading treatment if the current no-gradient requirement is retained.

## Verification/status

Navigation audit, mobile containment audit, license/secret audit, `git diff --check`, build, and tests passed (332 tests, 0 failures). The diff contains no route, auth, permission, API/client, DTO/contract, Blazorise, or LeptonX/module changes; the Bootstrap variable bridge remains scoped under `.hcs-app-shell`.

Status: DONE_WITH_CONCERNS
