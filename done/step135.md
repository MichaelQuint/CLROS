# Step 135 - App Executable Mapping Readiness Boundary

## What Changed

- Added `BootSequence.RunAppExecutableMappingReadiness`.
- Added stable readiness markers:
  - `APP_EXEC_MAPPING_READY`
  - `APP_EXEC_MAPPING_NOT_READY status=<status> descriptor=<status> payload=<status>`
- The boundary consumes `NativeAppPayloadMappingPlanResult` and records whether selected native app payload range planning is ready for a future executable mapping step.
- Added host tests for the ready marker, status-coded failure marker, and proof that the default boot marker sequence is unchanged.
- Updated boot, boot-order, app-model, and completion-gate docs with the evidence boundary.

## Why It Changed

Gitea issue #151 needed the next ordered boot boundary after selected payload range planning. This keeps native app launch work routed through `BootSequence` without prematurely allocating executable pages, changing page protections, relocating app bytes, or jumping to app code.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequenceAppExecutableMappingTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step135.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Full host tests emitted `TEST_PASS host.tests`.
- Host tests emitted `TEST_PASS boot.sequence app executable mapping ready`.
- Host tests emitted `TEST_PASS boot.sequence app executable mapping rejects mapping failure`.
- Host tests emitted `TEST_PASS boot.sequence app executable mapping preserves default markers`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0, with existing line-ending warnings.

## Failures

- A concurrent ImageBuilder worker temporarily introduced duplicate `NativeAppPackageBuilder` definitions while #151 was in progress. Parent integration resolved that out-of-scope overlap, then reran the build and host tests successfully.

## Deferred Work

- No executable page allocation was added.
- No live W^X/NX page permission transition was added.
- No payload byte copy, instruction-cache flush, relocation, or app entry invocation was added.
- The marker is not emitted by the no-stdlib EFI artifact or QEMU boot path yet.
- No hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.

## Evidence Boundary

This step is a managed host-tested boot-sequencing boundary only. It proves that selected native payload range-planning status can be converted into stable boot markers without altering the default boot order. It does not prove QEMU boot-time package loading, executable mapping, native-tier app execution, hosted .NET 10 startup, ASP.NET Minimal API execution, or Kestrel support.
