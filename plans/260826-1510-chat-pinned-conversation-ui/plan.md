# Chat pinned conversations and selection UX

## Objective

Move pinned conversations to the top of the chat list and improve the click-to-open experience with clear selection feedback, loading components, and restrained motion consistent with the existing chat design tokens.

## Scope

- Sort filtered conversations by pinned state first, then latest activity.
- Add semantic selected state to conversation buttons without changing the API contract.
- Add interaction and list-entry motion using transform/opacity, with reduced-motion support.
- Replace the empty message loading text with a lightweight chat skeleton while a selected conversation loads.
- Verify formatting and compile the Blazor client.

## Acceptance criteria

- Every pinned conversation appears before every unpinned conversation, including after pin/unpin and search filtering.
- Clicking a conversation gives immediate active feedback and the thread shows a loading component until messages resolve.
- Motion does not change layout, remains subtle, and is disabled for users who request reduced motion.
- Existing conversation actions, mobile behavior, and message loading remain intact.

## Verification

- `git diff --check` on changed files.
- `dotnet build src/HCS.Blazor/HCS.Blazor.csproj --no-restore`.
