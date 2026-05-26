# Step 709 - NativeAOT Cctor Work-Item Diagnostic Formatter

## Summary
Added a caller-owned diagnostic formatter for
`NativeAotClassConstructorWorkItemPlanResult`. The formatter emits the stable
`NATIVEAOT_CCTOR_WORK_ITEMS status=` marker into a caller-provided byte buffer,
reports required byte counts, rejects default/uninitialized plan results, and
returns before writing when the destination is too small.

## Why
The cctor work-item planner could produce value-type plan metadata, but the
future runtime diagnostic path still lacked a compact marker format for that
metadata. This step makes the work-item planning result inspectable without
executing class constructors, discovering metadata, invoking delegates, using
reflection, or relying on exception control flow.

## Files changed
- `src/OS.Kernel/Runtime/NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeAotClassConstructorWorkItemPlanTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/boot.md`
- `docs/completion-gates.md`
- `done/step709.md`

## Tests run
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `git diff --check`

## Evidence
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo` passed with 0
  warnings and 0 errors, proving the new kernel formatter compiles.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo` passed
  with 0 warnings and 0 errors after an unrelated active host-test process
  released the output DLL lock.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- "runtime.cctor work item"`
  exited 0 and emitted `TEST_PASS host.tests`.
- `git diff --check` passed; it printed only existing CRLF conversion warnings.
- Observed host-test markers:
  - `TEST_PASS runtime.cctor work item plans uninitialized entries`
  - `TEST_PASS runtime.cctor work item formatter formats success marker`
  - `TEST_PASS runtime.cctor work item formatter formats failure marker`
  - `TEST_PASS runtime.cctor work item formatter reports short destination`
  - `TEST_PASS runtime.cctor work item formatter rejects default result`
  - `TEST_PASS runtime.cctor work item layouts contain no managed references`

## Failures
- An earlier host-test build attempt was blocked by an unrelated
  `OS.HostTests.exe` process holding output DLLs. The process completed without
  intervention, and the rerun passed.

## Deferred work
- This remains a host-side formatter contract only. It does not wire
  `BootSequence`, QEMU marker presets, class-constructor invocation, metadata
  discovery, module initialization, exception handling, lazy static reference
  support, or NativeAOT helper exports.

## Evidence boundary
This step proves only kernel compilation and source formatting for the
caller-owned work-item diagnostic formatter. It does not prove full Gate 4
runtime correctness, class constructor execution, or QEMU/runtime-helper
behavior.
