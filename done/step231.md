# Step 231 - Workload Profile Tooling Propagation

## Summary

Propagated `OSWorkloadProfile` into host evidence tooling for QEMU smoke,
CI smoke, baseline evidence, QEMU runner dry-run/summary output, and raw image
planning.

## Why

Issue #252 needed the `SingleMinimalApi`/`MultiMinimalApi` selector to survive
the tooling paths that produce build, image, QEMU, and CI evidence. Without
that, a later agent could build with one profile while the evidence artifacts
failed to record which profile they represented.

## Files changed

- `scripts/run-qemu-smoke.ps1`
- `scripts/run-ci-smoke.ps1`
- `scripts/run-baseline-evidence.ps1`
- `tools/OS.QemuRunner/OS.QemuRunner.csproj`
- `tools/OS.QemuRunner/Program.cs`
- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/workload-profiles.md`
- `docs/qemu.md`
- `docs/image-builder.md`
- `done/step231.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers -WorkloadProfile MultiMinimalApi`
- `.\scripts\run-ci-smoke.ps1 -DryRun -WorkloadProfile MultiMinimalApi`
- `git diff --check`

## Evidence

- The first build attempt was blocked by a transient lock on
  `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll` and
  `OS.Std.NoRuntime.Shared.dll` from an `OS.HostTests` process. The process
  exited before follow-up verification.
- The follow-up solution build passed with `Build succeeded.`, `0 Warning(s)`,
  and `0 Error(s)`.
- Full host tests emitted `TEST_PASS host.tests`.
- Profile tooling host tests emitted:
  - `TEST_PASS ci.smoke.dryrun records default workload profile`
  - `TEST_PASS ci.smoke.dryrun propagates multi workload profile`
  - `TEST_PASS ci.smoke rejects invalid workload profile`
  - `TEST_PASS imagebuilder plan records default workload profile`
  - `TEST_PASS imagebuilder plan propagates multi workload profile`
  - `TEST_PASS imagebuilder rejects invalid workload profile`
  - `TEST_PASS qemu dry-run emits default workload profile`
  - `TEST_PASS qemu dry-run propagates multi workload profile`
  - `TEST_PASS qemu rejects invalid workload profile`
  - `TEST_PASS qemu smoke script dry-run propagates workload profile`
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers
  -WorkloadProfile MultiMinimalApi` emitted `WORKLOAD_PROFILE MultiMinimalApi`,
  `TEST_PASS workload.profile.tooling propagates multi`, and
  `TEST_PASS qemu.runner.dryrun`.
- `.\scripts\run-ci-smoke.ps1 -DryRun -WorkloadProfile MultiMinimalApi`
  emitted `WORKLOAD_PROFILE MultiMinimalApi`,
  `TEST_PASS workload.profile.tooling propagates multi`, and
  `TEST_PASS ci.smoke.dryrun`.
- `git diff --check` exited 0 with line-ending normalization warnings only.

## Failures

- The first build attempt failed because a transient `OS.HostTests` process
  held copied DLLs open. No compile failure was observed after the process
  exited.

## Deferred work

- This step does not implement hosted .NET 10, Kestrel, process tables,
  multi-app supervision, storage reads, live networking, or provider boot.
- `MultiMinimalApi` remains an evidence label and manifest validation mode
  until process/isolation/supervisor work exists.
