# Step 753 - Panic Text Buffer Shared Numeric Formatting

## Summary
Migrated `PanicTextBuffer` decimal and fixed-width hex output onto
`OS.Std.Text.AsciiTextBuilder`. The panic buffer now uses the shared
no-runtime ASCII formatting helper for unsigned decimal and `0x`-prefixed
64-bit uppercase hex fields instead of diagnostics-local digit loops.

## Why
Panic diagnostics are one of the boot/runtime paths that must avoid duplicate
integer-to-ASCII logic. Moving the formatter to the shared no-runtime helper
keeps future panic, serial, PAL, and protocol marker formatting aligned behind a
single caller-owned buffer contract.

## Files changed
- `src/OS.Kernel/Diagnostics/PanicTextBuffer.cs` now calls
  `AsciiTextBuilder.AppendUInt64Decimal` and `AppendUInt64Hex`, maps formatter
  failures to `PanicTextBufferStatus.Overflow`, and no longer carries a local
  hex digit helper.
- `tests/OS.HostTests/PanicTextBufferTests.cs` adds a source-guard marker that
  prevents reintroducing diagnostics-local decimal or hex loops.
- `docs/diagnostics.md` documents the shared panic numeric-formatting helper
  dependency and test marker.
- `docs/nativeaot-nostdlib-limits.md` documents the panic numeric formatter
  guard under the shared `AsciiTextBuilder` test evidence.
- `done/step753.md` records this evidence.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue711-panic-textbuilder\bin\Debug\net10.0`
- `dotnet .\artifacts\issue711-panic-textbuilder\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence
- Build log:
  `artifacts/logs/issue711-host-build-20260525.log`
- Host-test log:
  `artifacts/logs/issue711-hosttests-20260525.log`
- Diff-check log:
  `artifacts/logs/issue711-git-diff-check-final-20260525.log`
- Focused markers:
  - `TEST_PASS panic text buffer writes decimal and hex`
  - `TEST_PASS panic text buffer formats fault summary`
  - `TEST_PASS panic text buffer uses shared ascii whitespace helper`
  - `TEST_PASS panic text buffer uses shared ascii text builder`
  - `TEST_PASS host.tests`

## Failures
None in the focused build, full host-test harness, or `git diff --check`.
`git diff --check` emitted only existing LF/CRLF working-copy warnings.

## Deferred work
- Preserve exact panic-path behavior when the future live IDT/fault handler path
  wires `PanicTextBuffer` into serial output.
- Add live QEMU fault evidence for null-write and invalid-opcode probes after
  IDT/GDT/TSS/IST and owned COM1 invocation are implemented.
- Keep exception unwinding, hosted .NET 10, ASP.NET Minimal API, and Kestrel
  claims blocked until the corresponding completion gates have evidence.

## Evidence boundary
This is host-tested panic text formatting only. It does not install fault
handlers, capture live CPU registers, load descriptor tables, execute COM1
port-I/O stubs, write to real serial hardware, run a QEMU fault probe, or prove
hosted .NET/Kestrel behavior.

