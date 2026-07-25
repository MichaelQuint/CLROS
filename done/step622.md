# Step 622 - PAL Status Categories

## Summary

Added a small PAL status-category helper beside the existing status-name
formatter. `OSHostStatusCategoryFormatter` maps `OSHostStatus` values into
stable diagnostic buckets and formats canonical `OSHOST_STATUS_CATEGORY_*`
names into caller-owned buffers.

## Why

PAL diagnostics need a coarser status grouping than individual
`OSHostStatus` names so future status summaries can distinguish caller-shape
failures, resource failures, unsupported behavior, runtime unavailability, and
unknown/internal failures without allocation or exception-driven paths.

## Files changed

- `src/OS.Kernel/Pal/OSHostStatusCategory.cs`
- `tests/OS.HostTests/PalStatusCategoryTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `done/step622.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -o .\artifacts\hosttests-step622-shaped\bin\Debug\net10.0 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\artifacts\hosttests-step622-shaped\bin\Debug\net10.0\OS.HostTests.exe`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Shaped host-test build completed with 0 warnings and 0 errors.
- Full host-test log: `artifacts/logs/host-tests-step622.log`.
- Machine-readable markers:
  - `TEST_PASS pal.status_categories maps canonical statuses`
  - `TEST_PASS pal.status_categories formats category names`
  - `TEST_PASS pal.status_categories reports required bytes`
  - `TEST_PASS pal.status_categories rejects short buffer`
  - `TEST_PASS pal.status_categories maps unknown status`
  - `TEST_PASS pal.status_categories default result is not success`
  - `TEST_PASS host.tests`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` reported only Git CRLF conversion warnings; no whitespace
  errors were reported.

## Failures

- The normal host-test build output path was locked by existing
  `OS.HostTests` processes, so that command failed while copying DLLs into
  `tests/OS.HostTests/bin/Debug/net10.0`.
- A first alternate run from `artifacts/hosttests-step622` failed host-tool
  discovery tests because those tests infer configuration and target framework
  from `AppContext.BaseDirectory`. Rebuilding into the shaped
  `artifacts/hosttests-step622-shaped/bin/Debug/net10.0` path fixed that
  runner-only issue and the full host test pass succeeded.

## Deferred work

- Use the category helper in future PAL diagnostic summaries when a status
  table or serial marker needs coarse failure buckets.
- Keep exported PAL diagnostics separate; this helper is host-tested
  formatting/categorization groundwork only.

## Evidence boundary

This is host-tested PAL diagnostics helper evidence only. It does not export
unmanaged `OSHost_*` symbols, write serial output, expose diagnostics to a
hosted runtime, run hosted .NET 10, start Kestrel, or prove cloud readiness.
