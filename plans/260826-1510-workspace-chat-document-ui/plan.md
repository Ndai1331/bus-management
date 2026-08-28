# Workspace, chat and document UI polish

Status: completed

## Scope

- Center workspace event/document action controls vertically within their rows.
- Replace the workspace workflow list with an accessible status donut chart and legend.
- Keep the chat contacts tab at full panel height and pin the create-conversation action to the bottom.
- Localize survey-session statuses/actions and document statuses in both Vietnamese and English.
- Show the localized document status on the document detail page with a status badge.

## Implementation phases

1. `phase-01-ui-and-localization.md` — shared status helpers, Razor markup and CSS adjustments.
2. `phase-02-validation.md` — JSON validation, .NET build/type checks and focused review.

## Acceptance criteria

- Workspace event/document action controls are centered without changing the row height unexpectedly.
- Workflow statuses are represented by a responsive donut chart, with visible labels, counts and percentages.
- Contacts panel uses the available viewport height; the create button stays visible at the bottom while contacts scroll.
- No user-facing `Active`, `Closed`, or `DocumentStatus` enum values remain in the requested screens.
- Document detail displays a localized status badge for existing documents.
- Existing user changes remain untouched; no secrets or generated build artifacts are modified.
