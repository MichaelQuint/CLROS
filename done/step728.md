# Step 728 - Shared Decimal Formatting For Diagnostics Readiness

## Summary

Implemented Gitea issue #694 as a focused Phase B/C diagnostics/std migration
slice:

- added public signed and unsigned digit-count helpers to
  `OS.Std.Text.AsciiDecimalFormatter`;
- migrated `CpuFaultPanicReadinessFormatter` decimal byte counts and writes to
  the shared formatter;
- migrated `DiagnosticStubInvocationReadinessFormatter` address and byte-count
  decimal fields to the shared formatter;
- added host/source-guard tests for the shared formatter and both diagnostics
  formatters.

## Why

The diagnostics readiness formatters carried local decimal count/write loops for
panic and diagnostic-stub marker fields. Moving those numeric helpers into
`std/no-runtime/shared` keeps no-runtime-compatible numeric formatting in one
shared surface before more boot/panic paths depend on it.

## Files changed

- `std/no-runtime/shared/Text/AsciiDecimalFormatter.cs`
- `src/OS.Kernel/Diagnostics/CpuFaultPanicReadinessFormatter.cs`
- `src/OS.Kernel/Diagnostics/DiagnosticStubInvocationReadinessFormatter.cs`
- `tests/OS.HostTests/StdAsciiDecimalFormatterTests.cs`
- `tests/OS.HostTests/CpuFaultPanicReadinessFormatterTests.cs`
- `tests/OS.HostTests/DiagnosticStubInvocationReadinessFormatterTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/diagnostics.md`
- `done/step728.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build log: `artifacts\logs\issue694-host-build-20260525.log`
  - Result: `Build succeeded.`
  - Result: `0 Warning(s)`, `0 Error(s)`
- Host test log: `artifacts\logs\issue694-hosttests-20260525.log`
  - `TEST_PASS std.ascii.decimal.formatter reports required byte counts`
  - `TEST_PASS diagnostics.cpu_fault_panic formatter uses shared decimal formatter`
  - `TEST_PASS diagnostics.stub_invocation formatter uses shared decimal formatter`
  - `TEST_PASS host.tests`
- Whitespace log before docs/done update:
  `artifacts\logs\issue694-git-diff-check-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- No test failures were observed for this slice.
- No QEMU boot, live IDT, real CPU fault, live serial, panic-path execution, or
  post-`ExitBootServices` diagnostics evidence was produced.

## Deferred work

- The readiness formatters remain caller-owned host-tested marker formatters.
- Shared decimal formatting is not a complete `System.*` formatting surface and
  does not implement culture, padding, hex, floating-point, or allocation-based
  string formatting.
- Live CPU fault diagnostics still require the remaining IDT/GDT/TSS/IST,
  descriptor install, panic reporter, and QEMU fault-probe work tracked in
  `docs/completion-gates.md`.
