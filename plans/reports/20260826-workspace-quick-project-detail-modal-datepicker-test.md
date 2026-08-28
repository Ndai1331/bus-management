# Test Report — 2026-08-26 — Workspace quick project-detail modal/date-picker

## Scope

- [Workspace.razor](/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/Pages/Workspace.razor)
- [main.css](/Users/nguyenlong/Documents/Projects/bd-workspace/services/HCS_web_free_license/src/HCS.Blazor.Client/wwwroot/main.css)

The working tree was already dirty in other areas. No application source files were edited during this verification.

## Test results

### Requested build

Command:

```text
dotnet build src/HCS.Blazor.Client/HCS.Blazor.Client.csproj --no-restore
```

Result: PASS

- Build succeeded.
- 0 warnings, 0 errors.
- Elapsed: 10.88s.

### Related service tests

Command:

```text
dotnet test services/work-management/HCS.WorkManagementService.Tests/HCS.WorkManagementService.Tests.csproj --no-restore
```

Result: PASS

- Passed: 49
- Failed: 0
- Skipped: 0
- Total: 49
- Duration: 2s

This suite validates the related Work Management service, not rendered Blazor markup.

### Static checks

Command:

```text
git diff HEAD --check -- src/HCS.Blazor.Client/Pages/Workspace.razor src/HCS.Blazor.Client/wwwroot/main.css
```

Result: PASS; no whitespace errors.

Additional read-only checks confirmed:

- No Blazor client/UI test project or tests target `Workspace.razor` or `HcsDatePicker`.
- Blazorise 2.3.0 emits the `.datepicker .datepicker-calendar.dropdown-menu` structure targeted by the scoped rule.
- `hcs-tokens.css` loads before `main.css`; `--hcs-z-popover` is 350.
- The new modal localization keys exist in both `en.json` and `vi.json`.
- `.hcs-detail-split` is one column below 992px and two columns at 992px+.

## UI risks / unverified items

No browser smoke, visual, or accessibility run was available in this repository, so these remain manual verification items in an authenticated local `/workspace` session:

1. Open the date-range picker at 320, 768, 991, 992, and 1440px. Confirm the opaque calendar renders above the KPI/cards, remains clickable, and does not get clipped or cover unrelated navigation.
2. Open a project quick view and verify loading, populated, empty-members, empty-tasks, long-name/description, and many-task states. Confirm the nested task list scrolls cleanly on short mobile viewports.
3. Confirm the dynamic project title and `/project-detail/{id}` anchor behave correctly, and that task eye buttons, Escape, close, and focus return work as expected.
4. Long unbroken project/member names may still need runtime overflow checking; the title and member-name styles do not have the same explicit `overflow-wrap` rule as the read-only project value/description.
5. The date-picker fix depends on the rendered calendar remaining under `.hcs-ws-filter-dates`; runtime DOM/overflow behavior is not proven by compilation.

## Overall status

Build and related backend regression tests pass. The source changes are compile-safe, but final UI sign-off still requires the authenticated browser checks above.

