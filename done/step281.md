# Step 281 - Native App Exit Status Marker Contract

## What Changed

- Added `NativeAppExitStatusFormatter` as a host-tested native app exit marker
  formatter.
- Added bounded ASCII `APP_EXIT code=<n> status=<OSHOST_STATUS_*>` marker
  formatting for normal exits, nonzero exits, and launcher failures.
- Added deterministic rejection for invalid exit codes, invalid status values,
  and too-small destinations.
- Added log-table integration that writes the marker as a machine-readable
  stdout record through `OSHostAppLogTable`.
- Updated app-model, Kestrel, and completion-gate docs.

## Why It Changed

Future native app launcher and supervisor work need a stable exit marker before
any app code is executed. This step defines that marker and proves it can pass
through the host-tested app log table without claiming live app execution or
runner-observed exit status.

## Files Changed

- `src/OS.Kernel/Runtime/NativeAppExitStatus.cs`
- `tests/OS.HostTests/NativeAppExitStatusTests.cs`
- `docs/app-model.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step281.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

The solution build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The full host-test run emitted the expected issue markers:

```text
TEST_PASS appmodel.exit status formats success marker
TEST_PASS appmodel.exit status formats nonzero marker
TEST_PASS appmodel.exit status formats launcher failure
TEST_PASS appmodel.exit status rejects invalid input
TEST_PASS appmodel.exit status writes marker to app log
TEST_PASS host.tests
```

## Failures

No failures were observed. `git diff --check` exited successfully; it printed
existing LF-to-CRLF warnings only.

## Evidence Boundary

This is host-only marker-format evidence. It does not launch an app, observe a
real app exit, implement restart policy, expose a host-runner app result, start
hosted .NET 10, or prove ASP.NET Minimal API/Kestrel behavior.

## Deferred Work

- Add a native app lifecycle readiness marker boundary.
- Teach QEMU runner summaries to report future `APP_EXIT` markers.
- Connect the marker to the real launcher once app execution exists.
