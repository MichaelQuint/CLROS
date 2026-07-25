# Step 718 - PAL And HTTP Shared Helpers, App Selection Preset, Azure Parser Coverage

## Summary

Closed issue slices #655 through #658. PAL config/environment key lookup and
HTTP static response route matching now use the shared no-runtime memory
comparison helper. QEMU smoke tooling has an opt-in `app-selection` marker
preset for future native app selection images. Azure WireServer goal-state
parsing now has explicit host coverage for trimmed/case-insensitive tags,
malformed elements, duplicate tags, and size limits.

## Why

These are small implementation slices that keep Phase C utility ownership and
Phase G/H marker contracts moving without claiming live cloud readiness. Shared
byte-span comparison belongs in `std/no-runtime/shared` before PAL, networking,
and app-model code depend on it. The app-selection preset makes the future
guest evidence contract selectable without changing the default boot smoke
requirements. The Azure parser tests lock down current host-side provider
planning behavior before any provider import path can depend on it.

## Files changed

- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
- `src/OS.Kernel/Net/HttpStaticResponseTransaction.cs`
- `tests/OS.HostTests/HttpStaticResponseTransactionTests.cs`
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `tests/OS.HostTests/AzureWireServerGoalStateTests.cs`
- `done/step718.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-selection`
- `git diff --check`

## Evidence

- `artifacts/logs/issues655-658-host-build-20260525.log` records a successful
  host-test build with zero warnings and zero errors.
- `artifacts/logs/issues655-658-hosttests-20260525.log` records
  `TEST_PASS host.tests`, including:
  - `TEST_PASS pal.config table uses shared memory equality helper`
  - `TEST_PASS net.http.static uses shared memory equality helper`
  - `TEST_PASS cloud.azure.wireserver parses case-insensitive trimmed tags`
  - `TEST_PASS cloud.azure.wireserver rejects empty goal-state elements`
  - `TEST_PASS cloud.azure.wireserver rejects missing close tags`
  - `TEST_PASS cloud.azure.wireserver uses first duplicate goal-state tag`
  - `TEST_PASS cloud.azure.wireserver enforces goal-state size boundary`
  - `TEST_PASS qemu.smoke.preset app selection adds selection marker`
  - `TEST_PASS qemu.smoke.preset app selection aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes app selection preset`
- `artifacts/logs/issue657-qemu-app-selection-dryrun-20260525.log` records
  `QEMU_MARKER_PRESETS app-selection`, `MARKER_PRESETS app-selection`,
  `APP_SELECTION_READY`, and `TEST_PASS qemu.runner.dryrun`.
- `artifacts/logs/issues655-658-git-diff-check-20260525.log` records
  `EXIT_CODE 0`.

## Failures

No validation failures remained after the combined build and full host test run.
Earlier per-slice worker builds were temporarily blocked by the in-progress
`app-selection` QEMU test registrations before their helper methods landed.
That blocker was resolved before the combined validation. The final
`git diff --check` log still contains existing line-ending warnings about LF
files that Git will convert to CRLF on touch; it reported no whitespace errors.

## Deferred work

- The `app-selection` preset is marker-contract tooling only. It does not prove
  live native app launch, manifest-controlled workload execution, app restart
  policy, hosted .NET, Minimal API, or Kestrel.
- The PAL and HTTP helper migrations are host-tested code cleanup and source
  guard evidence only. They do not prove live OSHost ABI calls from CoreCLR,
  live TCP listen, virtio-net, or HTTP server execution in QEMU.
- The Azure WireServer tests are host-side parser coverage only. They do not
  prove Azure VM boot, WireServer reachability, cloud image import, or provider
  metadata integration.

