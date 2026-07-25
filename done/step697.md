# Step 697 - QEMU Cctor Preflight Marker Preset

## Summary
Added the opt-in `cctor-preflight` QEMU marker preset for future
runtime-focused smoke images. The preset requires
`NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY` in addition to the active baseline
markers and is wired through both `OS.QemuRunner` and
`scripts/run-qemu-smoke.ps1`.

## Why
The NativeAOT class-constructor runner preflight has host-tested diagnostic and
BootSequence readiness markers. QEMU tooling needed a targeted preset so future
runtime probe images can require that readiness marker without expanding the
default boot smoke gate or claiming class-constructor execution.

## Files changed
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tools/OS.QemuRunner/Program.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step697.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset cctor-preflight`
- `git diff --check -- src/OS.Kernel/Net/FetchAppHttpRequestEndpoint.cs tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs src/OS.Kernel/Boot/BootSequence.cs tests/OS.HostTests/NativeManifestParserTests.cs tools/OS.QemuRunner/SmokeMarkerPreset.cs tools/OS.QemuRunner/Program.cs scripts/run-qemu-smoke.ps1 tests/OS.HostTests/QemuRunnerTests.cs scripts/run-ci-smoke.ps1 tests/OS.HostTests/CiSmokeScriptTests.cs docs/networking.md docs/app-model.md docs/manifest.md docs/boot.md docs/qemu.md docs/nativeaot-nostdlib-limits.md docs/completion-gates.md done/step697.md done/step698.md done/step699.md done/step700.md`

## Evidence
- Host-test build passed with 0 warnings and 0 errors.
- Full host-test run exited 0 and preserved
  `artifacts/logs/parent-hosttests-20260525-050128.log`.
- The full host-test log emitted:
  - `TEST_PASS qemu.smoke.preset cctor preflight adds runtime readiness marker`
  - `TEST_PASS host.tests`
- The wrapper dry-run emitted:
  - `QEMU_MARKER_PRESETS cctor-preflight`
  - `MARKER_PRESETS cctor-preflight`
  - `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY`
  - `TEST_PASS qemu.runner.dryrun`
- Focused `git diff --check` exited 0. Git printed only line-ending
  normalization warnings for touched text files.

## Failures
- An earlier filtered host-test pipeline did not reach the global
  `TEST_PASS host.tests` marker. A later full host-test rerun exited 0 and
  reached `TEST_PASS host.tests`, so the preserved evidence for this step is
  the full log above.

## Deferred work
- This preset is marker-contract tooling only. It does not prove class
  constructors execute, static reference fields are fully supported, exception
  unwinding works, NativeAOT runtime correctness is complete, hosted .NET
  works, or Kestrel can run. A future real QEMU guest image must emit
  `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY` before this preset can serve as
  guest runtime evidence.

