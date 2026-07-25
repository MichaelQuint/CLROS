# Step 280 - Native App Stdout/Stderr Log Sink Contract

## What Changed

- Added `OSHostAppLogTable` as a host-tested in-process native app log sink
  contract.
- Added stdout and stderr stream ids plus a machine-readable record flag.
- Added bounded byte-record write/readback behavior with deterministic status
  codes for invalid stream ids, oversized records, invalid indexes, and record
  capacity exhaustion.
- Added host tests for stdout capture, stderr capture, invalid stream rejection,
  oversized write rejection, app status marker preservation, and deterministic
  exhaustion.
- Updated PAL, app-model, Kestrel, and completion-gate docs.

## Why It Changed

Future native app launch and hosted .NET/Kestrel work need a deterministic app
output boundary before any executable payload is invoked. This step defines the
first stdout/stderr capture contract without claiming live app execution, serial
routing, hosted .NET startup, or Kestrel logging.

## Files Changed

- `src/OS.Kernel/Pal/OSHostAppLogTable.cs`
- `tests/OS.HostTests/PalAppLogTableTests.cs`
- `docs/pal.md`
- `docs/app-model.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step280.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll | rg "pal\.app_log|TEST_FAIL|host\.tests"
```

## Logs And Evidence

The host-test project build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The focused host-test run emitted:

```text
TEST_PASS pal.app_log stdout write captured
TEST_PASS pal.app_log stderr write captured
TEST_PASS pal.app_log invalid stream rejected
TEST_PASS pal.app_log oversized write rejected
TEST_PASS pal.app_log preserves app status marker
TEST_PASS pal.app_log reports deterministic exhaustion
TEST_PASS host.tests
```

## Failures

No failures were observed in this slice.

## Evidence Boundary

This is host-only log sink contract evidence. It does not export
`OSHost_WriteLogBytes`, write guest serial output, launch app code, capture a
real app exit status, start hosted .NET 10, or prove ASP.NET Minimal API/Kestrel
logging behavior.

## Deferred Work

- Add a native app lifecycle readiness contract that consumes invocation
  readiness and log-sink readiness.
- Export and wire `OSHost_WriteLogBytes` when the PAL boundary is ready.
- Connect app stdout/stderr to serial/log policy after a real launcher exists.
