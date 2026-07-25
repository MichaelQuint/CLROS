# Step 137 - Native App Invocation Planning Contract

## What Changed

- Added `NativeAppInvocationPlanner` as a non-executing runtime/app-model planning contract.
- The planner consumes `NativeAppExecutableMemoryPlanResult` and `NativeAppLaunchPlanResult`.
- The result preserves executable-memory status, payload mapping status, descriptor/header status, launch status, stack byte requirements, entry offsets, executable mapping metadata, manifest metadata byte count, argument byte length, and environment count.
- Added status-coded failures for executable-memory plan failure, launch-plan failure, inconsistent executable-memory metadata, invalid entry offsets, invalid stack sizes, bounded manifest metadata failures, and range overflow.
- Added host-test source with the required markers:
  - `TEST_PASS appmodel native invocation plan accepts executable memory`
  - `TEST_PASS appmodel native invocation plan rejects memory failure`
  - `TEST_PASS appmodel native invocation plan rejects invalid stack`
  - `TEST_PASS appmodel native invocation plan preserves manifest metadata counts`
  - `TEST_PASS appmodel native invocation plan rejects invalid entry metadata`
- Updated app-model, no-stdlib, and completion-gate docs with the host-test-only boundary.

## Why It Changed

Gitea issue #155 needed the next deterministic metadata contract after executable-memory planning. This keeps native-tier app launch work status-coded and host-verifiable without allocating stacks, switching stacks, creating threads, mapping pages, finalizing an ABI, or executing app code.

## Files Changed

- `src/OS.Kernel/Runtime/NativeAppInvocationPlan.cs`
- `tests/OS.HostTests/NativeAppInvocationPlanTests.cs`
- `docs/app-model.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step137.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -p:OutputPath="<temp>\bin\"
```

## Logs And Evidence

- Focused host-test project build passed with 0 warnings and 0 errors using a temporary output directory outside the repository.
- The normal solution build reached compile after the code change, but could not complete because an existing `OS.HostTests` process held `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`.
- Full host-test execution was intentionally not run because host-test processes were already stacking up on this machine.

## Failures

- `dotnet build .\OS.slnx --nologo -m:1` failed with `MSB3027`/`MSB3021` because `OS.HostTests (5700)` locked the normal host-test output copy target.
- No QEMU, ImageBuilder, or full host-test verification was run for this step.

## Deferred Work

- No stack allocation was added.
- No stack switch, thread creation, page mapping, page-protection transition, payload copy, relocation, ABI finalization, or app entry invocation was added.
- No `BootSequence` executable-memory readiness or ImageBuilder command work was changed for this issue.
- The invocation plan is not emitted by the live EFI artifact or QEMU boot path.
- No hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.

## Evidence Boundary

This step is a managed metadata-planning boundary only. It proves by focused build that the new contract and host-test source compile, and it records the required host-test markers that will run through `OS.HostTests` when the current host-test process lock is cleared. It does not prove full host-test execution, QEMU boot-time package loading, executable mapping, stack allocation, native-tier app execution, hosted .NET 10 startup, ASP.NET Minimal API execution, or Kestrel support.
