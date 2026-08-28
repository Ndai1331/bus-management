# UI Review Fix Diagnosis

## Confirmed causes

- Mobile navigation left focusable siblings outside the drawer: the skip link and notification components were not part of the inert background boundary.
- Chat content padding was overridden by the later `main.css` rule at tablet widths because the corrective selector only existed below 768px.
- Page-specific CSS retained higher-specificity blue focus styles, so the shared teal focus token could not consistently win.
- Index and account page styles retained decorative gradients after the shared visual layer was introduced.

## Applied corrections

- Extended `inert` to the skip link and notification region while the drawer is open.
- Added a post-`main.css` chat full-bleed rule through 1100px.
- Replaced remaining affected focus styles with HCS semantic tokens.
- Replaced the affected gradients and shimmer backgrounds with solid surfaces and opacity pulses.
- Strengthened the navigation audit with mobile inert-boundary assertions.

## Verification target

Re-run the existing license, navigation, mobile-containment, build, and test commands after the patch.
