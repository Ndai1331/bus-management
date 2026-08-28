# Document signing performance fix

## Goal

Reduce the first-load latency of `/document-signing` without changing signing or authorization behavior.

## Diagnosis accepted

- The current database is small and `EXPLAIN (ANALYZE, BUFFERS)` is sub-millisecond for the document and workflow query shapes.
- The page awaits several independent HTTP calls serially.
- The workflow user lookup endpoint performs one Identity repository lookup per requested user ID.
- The document list eagerly loads three collections in one EF query and returns up to 100 documents while the page filters and pages locally.

## Implementation

1. Add a backward-compatible `GET /api/signing/queue` contract and endpoint. The Document service will filter running workflows and pending blocking tasks server-side, load only matching documents, use split collection queries, and return the page's queue DTO in one response.
2. Update the Blazor signing page to use the queue endpoint. Start independent initial-load requests concurrently and combine user-name and department lookups into one user request plus one organization request. Keep the page's existing fallback behavior.
3. Change Platform identity lookup to use `IIdentityUserRepository.GetListByIdsAsync` so one request produces one bulk Identity query. Add a small successful-result cache in `DocumentClient` to avoid repeating the same user lookup during a session.
4. Apply `AsSplitQuery()` to document aggregate reads and add targeted indexes for workflow/task/assignment/history access predicates through an EF migration.

## Compatibility and safety

- Existing document/workflow/signing endpoints remain unchanged.
- The new queue endpoint requires `Documents.Signing.Execute`, matching the page authorization.
- Do not run concurrent queries on a single EF `DbContext`; concurrency is only used between separate HTTP clients/scopes.
- Do not cache failed lookups or signing secrets.

## Verification

- Build the affected Document, Platform, and Blazor projects.
- Run document service and gateway contract tests plus the full solution tests if feasible.
- Inspect generated migration/model snapshot and run a read-only local migration check if the local runtime is available.
- Review the final diff for authorization, payload, and refresh behavior regressions.
