# Step 738 - Diagnostic Stub Callable Binding Plan

## Summary
Added a diagnostics-only, value-type callable-binding plan for generated COM1
diagnostic stubs. The plan sits after existing diagnostic-stub invocation
readiness and before any future live COM1 invocation.

## Why
The existing host contracts prove that generated COM1 `Out8`/`In8` stub
addresses resolve inside the executable payload range. The next Phase B
diagnostics slice needs a narrower pre-invocation gate that also checks the
materialized COM1 stub-byte evidence, COM1 base-port metadata, address/offset
consistency, full callable target ranges, overlap rejection, and the explicit
no-delegate/no-invocation/no-port-I/O boundary.

## Files changed
- `src/OS.Kernel/Diagnostics/DiagnosticStubCallableBindingPlan.cs`
- `tests/OS.HostTests/DiagnosticStubCallableBindingPlanTests.cs`
- `docs/diagnostics.md`
- `done/step738.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue2-diagnostics-callable-binding\bin\Debug\net10.0`
- `dotnet .\artifacts\issue2-diagnostics-callable-binding\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence
- Initial default-output build log: `artifacts/logs/issue2-diagnostics-callable-binding-host-build-20260525.log`
- Isolated shaped build log: `artifacts/logs/issue2-diagnostics-callable-binding-host-build-shaped-20260525.log`
- Host-test log: `artifacts/logs/issue2-diagnostics-callable-binding-hosttests-shaped-20260525.log`
- Diff-check log: `artifacts/logs/issue2-diagnostics-callable-binding-git-diff-check-full-20260525.log`
- Final diff-check log: `artifacts/logs/issue2-diagnostics-callable-binding-git-diff-check-final-20260525.log`
- Scoped diff-check log: `artifacts/logs/issue2-diagnostics-callable-binding-git-diff-check-scoped-20260525.log`
- New expected markers observed in the host-test log:
  - `TEST_PASS diagnostics.stub_callable binding accepts materialized callable targets`
  - `TEST_PASS diagnostics.stub_callable binding rejects failed invocation readiness`
  - `TEST_PASS diagnostics.stub_callable binding rejects backend mismatch`
  - `TEST_PASS diagnostics.stub_callable binding rejects invalid com1 base port`
  - `TEST_PASS diagnostics.stub_callable binding rejects target beyond materialized payload`
  - `TEST_PASS diagnostics.stub_callable binding rejects overlapping targets`
  - `TEST_PASS diagnostics.stub_callable binding default result is not success`
  - `TEST_PASS diagnostics.stub_callable binding result contains no managed references`

## Failures
- The first default-output host-test build failed at the final apphost copy
  because another `OS.HostTests.exe` process held
  `tests/OS.HostTests/bin/Debug/net10.0/OS.HostTests.exe` open. The shaped
  isolated build avoided that output path and passed with 0 warnings and 0
  errors.
- The full host-test executable from the shaped isolated build reached and
  passed the new diagnostics markers, then failed later in the existing
  `std.memoryextensions indexofany byte values` test:
  `IndexOfAny three values duplicate needles expected 1 but returned 0`. That
  failure is outside this diagnostics slice and was not edited here.

## Deferred work
- No `BootSequence` marker was added in this slice.
- No generated stub was executed.
- No managed delegate or unmanaged function pointer was created.
- No COM1 port I/O, serial write, QEMU boot proof, `ExitBootServices`, or cloud
  readiness evidence was produced.
- A future slice still needs to consume this value-only binding metadata in a
  real unmanaged callable binding path after executable mapping/cache-flush
  rules are defined.

## Evidence boundary
- Host-tested diagnostics contract only. This step proves value-only callable
  binding preconditions and rejection/default behavior before live COM1
  invocation. It does not prove live serial output, CPU port I/O, post-firmware
  diagnostics, QEMU boot behavior, storage, networking, hosted .NET, Kestrel, or
  cloud readiness.
