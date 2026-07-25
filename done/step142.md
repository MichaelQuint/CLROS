# Step 142 - BootSequence Native App Invocation Readiness Boundary

## What Changed

- Added `BootSequence.RunAppInvocationReadiness` as a host-tested marker boundary for `NativeAppInvocationPlanResult`.
- Added stable marker constants and formatter support for:
  - `APP_INVOCATION_READY`
  - `APP_INVOCATION_NOT_READY status=<status> memory=<memory-status> launch=<launch-status>`
- Added host-test source with the required issue #160 markers:
  - `TEST_PASS boot.sequence app invocation ready`
  - `TEST_PASS boot.sequence app invocation rejects plan failure`
  - `TEST_PASS boot.sequence app invocation preserves default markers`
- Updated boot, boot-order, app-model, and completion-gate docs with the host-test-only evidence boundary.

## Why It Changed

Gitea issue #160 needed the next native app boot-boundary marker after executable-memory and invocation metadata planning. This step proves the boot sequence can consume the invocation planning result and emit deterministic readiness or blocked markers without executing app code.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequenceAppInvocationTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step142.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors after an initial concurrent-build file lock was cleared by rerunning the host-test build serially.
- The no-build host-test run emitted the issue #160 markers before it was stopped on request:
  - `TEST_PASS boot.sequence app invocation ready`
  - `TEST_PASS boot.sequence app invocation rejects plan failure`
  - `TEST_PASS boot.sequence app invocation preserves default markers`

## Failures

- An initial parallel host-test build failed with `CS2012` because `OS.Kernel.dll` was locked while the kernel project build was still writing it. The build passed when rerun serially.
- Full host-test execution was intentionally stopped after the user requested that broad host-test verification stop. The issue #160 markers had already emitted before the stop.

## Deferred Work

- No stack allocation, stack switching, thread creation, page mapping, page-protection changes, final ABI definition, relocation, native app entry invocation, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.
- The helper is not wired into the default `BootSequence.Run` marker order or the live no-stdlib EFI artifact.
- The Gitea issue was not closed.

## Evidence Boundary

This step is managed host-test marker evidence only. It proves that `NativeAppInvocationPlanResult` can be reduced to stable boot-readiness markers and that the default `BootSequence.Run` marker order is preserved. It does not prove live boot-time package reads, executable memory mapping, stack allocation, thread launch, a final app ABI, native app execution, hosted .NET 10, ASP.NET Minimal API, Kestrel, or QEMU firmware-visible app invocation markers.
