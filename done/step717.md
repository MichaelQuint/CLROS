# Step 717 - App Args CI Lane, Manifest Shared ASCII, Exit Prefix, App Lifecycle Preset

## Summary

Closed issue slices #651 through #654. The CI smoke dry-run now includes the
`app-args` QEMU marker-preset lane, the native manifest parser uses the shared
no-runtime ASCII equality helper, native app exit-status parsing uses the shared
memory prefix helper, and QEMU smoke tooling has an opt-in `app-lifecycle`
marker preset for future native app lifecycle images.

## Why

The CloudReady path needs small, testable contracts that can move from
host-tested tooling into real guest evidence without changing default boot
requirements. The `app-args` CI lane keeps argument-vector smoke coverage in the
artifact plan. The `app-lifecycle` marker preset creates a stable requirement
set for images that can prove native app lifecycle readiness later. The parser
migrations continue the Phase C rule that shared string/memory behavior belongs
under `std/no-runtime/shared` before kernel and app-model usage.

## Files changed

- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `src/OS.Kernel/Runtime/NativeAppExitStatus.cs`
- `tests/OS.HostTests/NativeAppExitStatusTests.cs`
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step717.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-ci-smoke.ps1 -DryRun`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-lifecycle`
- `git diff --check`

## Evidence

- `artifacts/logs/issues651-654-host-build-20260525.log` records a successful
  host-test build with zero warnings and zero errors.
- `artifacts/logs/issues651-654-hosttests-20260525.log` records
  `TEST_PASS host.tests`, including:
  - `TEST_PASS ci.smoke.dryrun includes app-args marker preset`
  - `TEST_PASS manifest.native parser uses shared ascii equality helper`
  - `TEST_PASS appmodel.exit status parser uses shared memory prefix helper`
  - `TEST_PASS qemu.smoke.preset app lifecycle adds lifecycle marker`
  - `TEST_PASS qemu.smoke.preset app lifecycle aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes app lifecycle preset`
- `artifacts/logs/issue651-ci-smoke-dryrun-20260525.log` records
  `TEST_PASS ci.smoke.dryrun includes app-args marker preset` and
  `TEST_PASS ci.smoke.dryrun`.
- `artifacts/logs/issue654-qemu-app-lifecycle-dryrun-20260525.log` records
  `QEMU_MARKER_PRESETS app-lifecycle`, `MARKER_PRESETS app-lifecycle`,
  `APP_LIFECYCLE_READY`, and `TEST_PASS qemu.runner.dryrun`.
- `artifacts/logs/issues651-654-git-diff-check-20260525.log` records
  `EXIT_CODE 0`.

## Failures

No validation failures remained after the combined build/test run. The
`git diff --check` log still contains existing line-ending warnings about LF
files that Git will convert to CRLF on touch; it reported no whitespace errors.

## Deferred work

- The `app-args` CI lane and `app-lifecycle` preset are marker-contract tooling
  only. They do not prove live native app launch, manifest-controlled restart,
  app supervisor isolation, hosted .NET, Minimal API, or Kestrel.
- The parser migrations are host-tested shared-helper migrations only. They do
  not prove live firmware boot, ExitBootServices, virtio storage, or networking.

