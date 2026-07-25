# Step 283 - OSHost WriteLogBytes-Shaped App Log Facade

## What Changed

- Added `OSHostWriteLogBytesRequest` as the POD request shape for future
  `OSHost_WriteLogBytes` calls.
- Added `OSHostAppLogFacade` as an in-process facade over
  `OSHostAppLogTable`.
- Added host tests for stdout writes, stderr writes, invalid stream rejection,
  oversized writes, record-table exhaustion, and machine-readable marker flag
  preservation.
- Updated PAL, Kestrel, and completion-gate docs.

## Why It Changed

Future hosted .NET and ASP.NET logging need a flat PAL-shaped logging boundary.
This step proves the request shape and status translation without exporting an
unmanaged `OSHost_WriteLogBytes` symbol or routing logs to guest serial output.

## Files Changed

- `src/OS.Kernel/Pal/OSHostAbi.cs`
- `src/OS.Kernel/Pal/OSHostAppLogFacade.cs`
- `tests/OS.HostTests/PalAbiLayoutTests.cs`
- `tests/OS.HostTests/PalAppLogFacadeTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step283.md`

## Tests Run

```powershell
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll | rg "pal\.app_log facade|TEST_FAIL|host\.tests"
```

## Logs And Evidence

The combined host-test run emitted:

```text
TEST_PASS pal.app_log facade writes stdout
TEST_PASS pal.app_log facade writes stderr
TEST_PASS pal.app_log facade rejects invalid stream
TEST_PASS pal.app_log facade rejects oversized write
TEST_PASS pal.app_log facade reports exhaustion
TEST_PASS pal.app_log facade preserves machine marker flag
TEST_PASS host.tests
```

## Failures

No failures were observed for this slice in the combined host-test run.

## Evidence Boundary

This is host-only facade evidence. It does not export `OSHost_WriteLogBytes`,
bind hosted runtime console streams, write guest serial output, launch app code,
observe app exit status, or prove ASP.NET Minimal API/Kestrel logging behavior.

## Deferred Work

- Export the real `OSHost_WriteLogBytes` PAL symbol when hosted runtime linking
  reaches this boundary.
- Route app logs to guest serial/log policy after the live launcher exists.
