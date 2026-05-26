# Step 583 - PAL app-log table default-result guards

## Summary

`OSHostAppLogWriteResult` and `OSHostAppLogReadResult` now carry
initialized-state metadata and require it for `Succeeded`. Default/zero PAL
app-log write/read results can no longer be interpreted as successful log
evidence.

## Why

The app-log table is the host-tested stdout/stderr sink contract for future app
launcher and PAL log routing work. Its default result structs had
success-shaped `OSHostStatus.Ok` values. This step makes the result boundary
explicit while keeping the implementation host-only and in-process.

## Files changed

- `src/OS.Kernel/Pal/OSHostAppLogTable.cs`
- `tests/OS.HostTests/PalAppLogTableTests.cs`
- `docs/pal.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step583.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.app_log|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build succeeded with `0 Warning(s)` and `0 Error(s)`.
- Filtered host-test markers:
  - `TEST_PASS pal.app_log stdout write captured`
  - `TEST_PASS pal.app_log stderr write captured`
  - `TEST_PASS pal.app_log invalid stream rejected`
  - `TEST_PASS pal.app_log oversized write rejected`
  - `TEST_PASS pal.app_log preserves app status marker`
  - `TEST_PASS pal.app_log reports deterministic exhaustion`
  - `TEST_PASS pal.app_log default results are not success`
  - `TEST_PASS pal.app_log facade writes stdout`
  - `TEST_PASS pal.app_log facade preserves machine marker flag`
  - `TEST_PASS host.tests`
- Full solution build succeeded with `0 Warning(s)` and `0 Error(s)`.
- Naming/source firewall emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited `0`; output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- None.

## Deferred work

- App-log coverage remains host-tested in-process table/facade evidence only.
  It does not export PAL log functions, route live app stdout/stderr, connect
  hosted runtime console streams, capture real app exits, start hosted .NET,
  start Kestrel, prove cloud readiness, or prove production readiness.
