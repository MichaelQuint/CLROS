# Step 772 - CPU fault panic formatter shared ASCII builder

## Summary
Migrated `CpuFaultPanicReadinessFormatter` to use the shared
`OS.Std.Text.AsciiTextBuilder` append path for ready/not-ready marker bytes and
decimal fields. The existing `CPU_FAULT_PANIC_READY` and
`CPU_FAULT_PANIC_NOT_READY ...` marker text, required-byte query behavior,
maximum-marker guard, invalid-status handling, short-destination behavior, and
default-result rejection remain unchanged.

## Why
CPU fault panic readiness markers are panic-adjacent diagnostics. Keeping
their caller-owned formatting on the shared no-runtime ASCII builder reduces
local formatting code before the live IDT/fault-handler path starts invoking
panic diagnostics in QEMU.

## Files changed
- `src/OS.Kernel/Diagnostics/CpuFaultPanicReadinessFormatter.cs`
- `tests/OS.HostTests/CpuFaultPanicReadinessFormatterTests.cs`
- `docs/diagnostics.md`
- `done/step772.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue730-cpu-fault-panic-formatter\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-host-build-20260525.log`
- `dotnet .\artifacts\issue730-cpu-fault-panic-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-hosttests-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-git-diff-check-20260525.log`

## Evidence
- Build log: `artifacts/logs/issue730-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue730-hosttests-20260525.log`
  - `TEST_PASS diagnostics.cpu_fault_panic formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Integrated host-test evidence in the same run also covered sibling
  formatter migrations:
  - `TEST_PASS network http client transaction formatter uses shared ascii text builder`
  - `TEST_PASS runtime.cctor runner preflight formatter uses shared ascii text builder`
  - `TEST_PASS storage virtio block package file staging formatter uses shared ascii text builder`
- Source guard evidence:
  - `CpuFaultPanicReadinessFormatter.cs` still uses
    `AsciiDecimalFormatter.CountInt32Digits(...)` for required-byte counts.
  - It now uses `AsciiTextBuilder.AppendBytes(destination, 0, ReadyMarker)`
    and `AsciiTextBuilder.AppendBytes(destination, offset, source)` for marker
    text.
  - It now uses `AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)`
    for decimal writes.
  - The guard rejects `AsciiDecimalFormatter.FormatInt32(...)`,
    `destination.Slice(offset)`, direct `ReadyMarker.CopyTo(destination)`,
    direct `source.CopyTo(destination...)`, and diagnostics-local digit loops.
- Source check: `artifacts/logs/issue730-git-diff-check-20260525.log`
  exited 0 with only existing LF-to-CRLF working-copy warnings.

## Failures
- None in this slice.
- No QEMU smoke was run because this change is host-tested diagnostics
  formatting only.

## Deferred work
- Wire the formatter into live CPU exception handlers once IDT/GDT/TSS/IST and
  panic-safe owned serial output are installed in the boot path.
- Add QEMU null-write and invalid-opcode panic evidence only after the live
  handler path exists.

## Evidence boundary
This step proves caller-owned host formatting of already-produced CPU fault
panic readiness results through the shared no-runtime ASCII text builder. It
does not install a live IDT, execute fault handlers, capture CR2/CR3/registers
from real CPU exceptions, emit a QEMU panic dump, or provide cloud-provider
evidence.

