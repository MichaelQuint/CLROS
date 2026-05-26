# Step 653 - Diagnostic Stub Invocation Marker Formatter

## Summary

Added a caller-owned ASCII marker formatter for diagnostic-stub invocation
readiness. The formatter produces the same ready/not-ready marker text used by
the managed `BootSequence` helper, reports short destination buffers with the
required byte count, rejects invalid nested status values before writing, and
keeps the result layout value-only for future panic-safe integration.

## Why

`DiagnosticStubInvocationReadiness` gates any future COM1 diagnostic stub call
on executable payload range checks and resolved backend addresses, but the
marker path still depended on managed string formatting. The next diagnostics
steps need a no-allocation, caller-storage formatter before that readiness can
move closer to the panic/serial path.

## Files changed

- `src/OS.Kernel/Diagnostics/DiagnosticStubInvocationReadinessFormatter.cs`
  - Added `DiagnosticStubInvocationMarkerFormatStatus`.
  - Added `DiagnosticStubInvocationMarkerFormatResult`.
  - Added ready, not-ready, and required-byte marker formatting helpers.
- `tests/OS.HostTests/DiagnosticStubInvocationReadinessFormatterTests.cs`
  - Added coverage for ready marker formatting, not-ready marker formatting,
    destination capacity reporting, invalid nested status rejection, and
    default result rejection.
- `docs/diagnostics.md`
  - Documented the caller-owned marker formatter behavior and current
    host-only evidence boundary.
- `docs/completion-gates.md`
  - Updated the diagnostics gate evidence and limitations to include the
    formatter.
- `done/step653.md`
  - Recorded this milestone and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
  - Result: passed.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Result: passed.
  - Log: `artifacts/logs/host-tests-stub-invocation-formatter-final.log`.

## Evidence

- `TEST_PASS diagnostics.stub_invocation formatter formats ready marker`
- `TEST_PASS diagnostics.stub_invocation formatter formats not-ready marker`
- `TEST_PASS diagnostics.stub_invocation formatter reports short destination`
- `TEST_PASS diagnostics.stub_invocation formatter rejects invalid status`
- `TEST_PASS diagnostics.stub_invocation formatter default result is not success`
- `TEST_PASS host.tests`

## Failures

- The first full host-test attempt during this slice failed before reaching the
  formatter tests because a stale `OS.QemuRunner.exe` process held
  `tools/OS.QemuRunner/bin/Debug/net10.0/OS.QemuRunner.exe` while nested CI
  smoke dry-run tests tried to rebuild the runner. The stale process exited,
  and the same full host-test command was rerun successfully.
- This is host-test marker-format evidence only. It does not execute generated
  diagnostic stubs, perform CPU port I/O, change page permissions, flush
  instruction caches, or prove QEMU-visible owned COM1 output.

## Deferred work

- Wire the formatter into a real panic/serial path once caller-owned diagnostic
  logging replaces the current managed boot marker path.
- Implement real executable-stub page transitions and only then add live
  unmanaged function-pointer invocation of the generated COM1 stubs.
- Add QEMU fault-probe evidence after IDT/GDT/TSS/IST setup and owned serial
  panic output exist.
