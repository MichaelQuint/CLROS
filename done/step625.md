# Step 625 - CPU Fault Panic Readiness Formatter

## Summary

Added a host-testable caller-owned formatter for CPU fault panic readiness
markers. The formatter writes `CPU_FAULT_PANIC_READY` and
`CPU_FAULT_PANIC_NOT_READY ...` text into supplied ASCII byte buffers, reports
required byte counts for short destinations, and rejects unknown nested status
values before writing.

## Why

The existing CPU fault panic readiness result carries bounded frame/reporter
metadata, and `BootSequence.RunCpuFaultPanicReadiness` emits host-tested marker
strings. A future panic-safe path needs the same marker shape available without
string allocation or managed formatting, while still keeping real IDT handlers,
CPU fault capture, and COM1 panic output out of scope.

## Files changed

- `src/OS.Kernel/Diagnostics/CpuFaultPanicReadinessFormatter.cs`
- `tests/OS.HostTests/CpuFaultPanicReadinessFormatterTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step625.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -p:OSWorkloadProfile=SingleMinimalApi`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- The targeted host-test build with `OSWorkloadProfile=SingleMinimalApi`
  completed with 0 warnings and 0 errors.
- Full host tests emitted:
  - `TEST_PASS diagnostics.cpu_fault_panic formatter formats ready marker`
  - `TEST_PASS diagnostics.cpu_fault_panic formatter formats not-ready marker`
  - `TEST_PASS diagnostics.cpu_fault_panic formatter reports short destination`
  - `TEST_PASS diagnostics.cpu_fault_panic formatter rejects invalid status`
  - `TEST_PASS diagnostics.cpu_fault_panic formatter default result is not success`
  - `TEST_PASS host.tests`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0.

## Failures

- The plain host-test build without `OSWorkloadProfile` failed in the existing
  workload-profile guard with `Invalid OSWorkloadProfile ''. Valid values:
  SingleMinimalApi, MultiMinimalApi.` The same targeted build passed after
  specifying `SingleMinimalApi`.
- `git diff --check` printed Git LF-to-CRLF conversion warnings for existing
  dirty files, but reported no whitespace errors and exited 0.

## Deferred work

- Wire the caller-owned formatter into the boot marker helper only when
  `BootSequence.cs` is in scope and the existing marker text can be preserved.
- Wire the readiness path into real interrupt-stub register capture and owned
  serial panic output only after IDT/GDT/TSS/IST installation and COM1 backend
  execution have matching evidence.

## Evidence boundary

This is host-testable formatter evidence only. It does not install IDT, GDT,
TSS, or IST state, execute descriptor-table or port-I/O stubs, trigger CPU
faults, capture live registers, write through real COM1 hardware, call
`ExitBootServices`, or prove QEMU fault handling.

