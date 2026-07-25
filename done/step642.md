# Step 642 - CI Diagnostics Marker Dry-Run Artifact

## Summary
Added a CI smoke wrapper lane that records the QEMU diagnostics marker-preset dry-run as its own artifact set. The lane is host-only and keeps real QEMU/libvirt optional.

## Why
Issue #7 needs CI/evidence coverage that the diagnostics marker preset is captured by the top-level smoke wrapper without changing OS.QemuRunner preset logic, diagnostics internals, PAL, networking, or libvirt image-format validation.

## Files changed
- `scripts/run-ci-smoke.ps1`
  - Added `ci.smoke.diagnostics.dryrun`.
  - Captures `qemu-diagnostics-dry-run.log`, `qemu-diagnostics-serial.log`, and `qemu-diagnostics.summary.log`.
  - Emits `TEST_PASS ci.smoke.dryrun includes diagnostics marker preset` in dry-run artifact-plan validation.
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
  - Verifies the CI artifact plan includes the diagnostics dry-run lane, `-MarkerPreset diagnostics`, serial and summary artifacts, and workload-profile propagation.
  - Verifies the executed CI wrapper log contains the diagnostics preset and ordered marker chain.
- `docs/qemu.md`
  - Documents the diagnostics marker-preset dry-run lane and its evidence boundary.
- `docs/completion-gates.md`
  - Records the CI diagnostics marker-preset dry-run as host-tooling evidence for Gate 12.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Passed with `TEST_PASS host.tests`.
  - Log: `artifacts/logs/host-tests-step642.log`.
- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\step642-ci-smoke-dryrun`
  - Passed with `TEST_PASS ci.smoke.dryrun`.
- `.\scripts\run-ci-smoke.ps1 -LogRoot artifacts\logs\step642-ci-smoke-execute`
  - Passed with `TEST_PASS ci.smoke`.

## Evidence
- Dry-run wrapper marker: `TEST_PASS ci.smoke.dryrun includes diagnostics marker preset`.
- Executed wrapper lane marker: `TEST_PASS ci.smoke.diagnostics.dryrun`.
- `artifacts/logs/step642-ci-smoke-execute/ci-smoke.artifacts.json` includes:
  - `ci.smoke.diagnostics.dryrun`.
  - `-MarkerPreset diagnostics`.
  - `qemu-diagnostics-dry-run.log`.
  - `qemu-diagnostics-serial.log`.
  - `qemu-diagnostics.summary.log`.
- `artifacts/logs/step642-ci-smoke-execute/qemu-diagnostics-dry-run.log` includes:
  - `QEMU_MARKER_PRESETS diagnostics`.
  - `MARKER_PRESETS diagnostics`.
  - `DIAGNOSTICS_CHAIN_READY`.
  - `ORDERED_MARKERS FAULT_INFRA_READY;EARLY_DIAGNOSTICS_READY;DIAGNOSTIC_STUBS_MATERIALIZED;CONTROLLED_FAULT_PROBES_READY;CPU_FAULT_PANIC_READY;DIAGNOSTICS_CHAIN_READY`.
  - `TEST_PASS qemu.runner.dryrun`.

## Failures
- The first host-test build attempt failed because a stale `OS.HostTests.exe`/`dotnet run` process held the default output DLL open.
- Alternate-output build experiments exposed existing generated-artifact source inclusion issues in this repo shape. Those generated step642 artifact directories were removed, the stale host-test process was stopped, the MSBuild build server was shut down, and the normal focused build/test path passed.

## Deferred work
- No real QEMU or libvirt execution was added or required.
- This does not prove live IDT/fault behavior, ExitBootServices behavior, or diagnostics implementation correctness; it only proves the CI wrapper captures the diagnostics marker-preset dry-run evidence as an artifact.
- No Gitea update was posted for this slice, per task instruction.

