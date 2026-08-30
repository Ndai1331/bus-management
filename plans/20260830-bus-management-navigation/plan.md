# Bus Management navigation

## Objective

Expose the implemented Bus Management bounded context from the HCS Blazor shell so authorized users can discover the dashboard and the v1 operational areas without browser-to-service calls or broken links.

## Scope

- Add a permission-aware Bus Management dropdown to `HCSMainLayout`.
- Use the existing BFF route and dashboard as the v1 landing page.
- Add localized labels and a route catalog entry for Bus Management.
- Keep the existing teal shell, keyboard behavior, mobile drawer, and active-link styling.
- Verify with focused source checks and a Blazor build.

## Acceptance criteria

- Users with any Bus Management permission see the menu; unauthorized users do not.
- The dashboard link is available at `/bus-management` and remains protected by the existing page policy.
- Menu labels exist in Vietnamese and English.
- The menu works with click, keyboard, pointer, and mobile navigation behavior already provided by the shell.
- No direct browser link targets `https://localhost:44416`.

## Status

Implementation and testing complete.

### Dashboard and menu permission decision

- The Bus Management menu and `/bus-management` dashboard are available to authenticated users with any `HCS.BusManagement.*` permission claim, including the dedicated `HCS.BusManagement.Dashboard` permission.
- The dashboard remains protected by the `HCS.BusManagement.Dashboard` policy; operational and reporting links retain their own permission policies.

### Validation note

- Focused source checks and the Blazor build are complete.
- Browser end-to-end validation remains limited by the browser not trusting the local HTTPS certificate/CA for `https://localhost:44416`. This is an environment limitation and remains unresolved.
