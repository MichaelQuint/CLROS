# Step 687 - Native APP_EXIT Observer Diagnostic Formatter

## Summary

Added a caller-owned ASCII diagnostic formatter for
`NativeAppExitLogObserverResult`. The formatter writes stable
`APP_EXIT_OBSERVER status=...` metadata lines, supports required-byte queries,
reports short destinations without partial writes, and rejects
default/uninitialized observer results.

## Why

Future app supervisor and smoke-runner work needs a kernel-side diagnostic
surface for observer results that is not tied to host string formatting,
exceptions, allocations, or live app execution. This slice makes the
host-tested observer result directly serial/log friendly while keeping the
evidence boundary honest.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppExitLogObserverDiagnosticFormatter.cs`
  - Added `NativeAppExitLogObserverDiagnosticFormatStatus`.
  - Added `NativeAppExitLogObserverDiagnosticFormatResult` with initialized
    state, observer status, byte counts, destination capacity, and
    default-result rejection.
  - Added `FormatResult` and `QueryRequiredBytes` for
    `APP_EXIT_OBSERVER status=` diagnostics covering observer status,
    records scanned, record index, stream id, flags, sequence, bytes read,
    required bytes, log status, parse attempt/status, exit code/status, and
    launcher-failure metadata.
- `tests/OS.HostTests/NativeAppExitLogObserverTests.cs`
  - Added formatter coverage for success, short destinations, and default
    observer-result rejection.
- `docs/app-model.md`
  - Documented the formatter and host-only evidence boundary.
- `docs/completion-gates.md`
  - Added host-test evidence while keeping native app execution and exit
    capture gates failed.
- `done/step687.md`
  - Recorded this slice.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "appmodel\.exit observer formatter|host\.tests|TEST_FAIL"`

## Evidence

- Host-test build succeeded with 0 errors.
- Filtered host-test run exited 0 and emitted:
  - `TEST_PASS appmodel.exit observer formatter formats success marker`
  - `TEST_PASS appmodel.exit observer formatter reports short destination`
  - `TEST_PASS appmodel.exit observer formatter rejects default result`
  - `TEST_PASS host.tests`

## Failures

- The host-test build emitted MSB3026 retry warnings because another
  `OS.HostTests.exe` process briefly held copied test-output assemblies. The
  retry completed and the build succeeded with 0 errors.

## Deferred work

- Connect the observer and formatter to future supervisor-owned app log
  ingestion after live native app launch and stdout/stderr routing exist.
- Emit formatter output over the guest serial/log path once app-exit capture is
  part of the live boot path.
- Keep QEMU runner `APP_EXIT_RESULT` summaries separate until guest app
  execution is proven.

## Evidence boundary

This is host-tested diagnostic formatting evidence only. It does not prove live
native app launch, live stdout/stderr routing, supervisor restart policy,
serial-log ingestion, executable mapping, hosted .NET 10, ASP.NET Minimal API,
Kestrel, cloud readiness, or production readiness.
