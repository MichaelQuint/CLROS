# Step 128 - BootSequence Native Payload Readiness Marker Boundary

## What Changed

- Added `APP_PAYLOAD_READY` and `APP_PAYLOAD_NOT_READY` boot marker constants.
- Added `BootSequence.RunAppPayloadReadiness`, a host-testable helper that consumes `NativeAppLaunchDescriptorResult` and reports descriptor plus payload-header status without executing app code.
- Added app-payload readiness host tests for ready, descriptor-failure, invalid-header, and default-marker unchanged behavior.
- Updated boot, boot-order, app-model, and completion-gate docs to describe the payload readiness boundary and evidence limits.

## Why It Changed

Issue #147 needed a marker boundary after selected app descriptor creation but before executable mapping or app entry. The new helper makes payload readiness explicit and keeps default boot markers unchanged.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequenceAppLaunchDescriptorTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step128.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS boot.sequence app payload ready`
  - `TEST_PASS boot.sequence app payload rejects descriptor failure`
  - `TEST_PASS boot.sequence app payload rejects invalid header`
  - `TEST_PASS boot.sequence app payload leaves default markers unchanged`
  - `TEST_PASS host.tests`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited successfully. It printed existing line-ending normalization warnings.

## Failures

- None in this step.

## Deferred Work

- No executable mapping, relocation, stack allocation, app ABI entry, scheduler/thread startup, stdout/stderr routing, exit-code capture, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel behavior was added.
- The payload marker boundary is not wired into the default boot path or live EFI artifact.

## Evidence Boundary

This step proves only a managed, host-testable status aggregation boundary for selected native payload readiness. It does not prove native app execution, memory permissions, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
