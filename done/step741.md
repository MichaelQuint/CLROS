# Step 741 - App entry staging preflight gate

## Summary

Added a value-only native app entry staging preflight that requires the existing
package-staging readiness result before `APP_ENTRY_READY` can be emitted. The
boot marker now records staging, package-file staging, and payload staging
status fields when the final app-entry boundary is blocked.

## Why

The app-entry readiness marker previously accepted manifest/package/descriptor,
payload mapping, executable-memory, and invocation prerequisites directly. This
left the final pre-entry boundary able to skip the caller-owned package-file and
payload staging evidence that Phase F/G app-model/storage work needs before any
future launcher treats app bytes as ready.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppEntryStagingPreflight.cs`
  - New value-only result/status and adapter from
    `NativeAppPackageStagingReadinessResult`.
  - Requires initialized caller-owned package-file staging and payload staging
    byte evidence.
  - Preserves no-live-storage, no-allocation, no-executable-map, no-permission,
    and no-app-execution boundary flags.
- `src/OS.Kernel/Boot/BootSequence.cs`
  - `RunAppEntryReadiness` now requires `NativeAppEntryStagingPreflightResult`.
  - `APP_ENTRY_NOT_READY` includes `staging=`, `packageStaging=`, and
    `payloadStaging=` metadata.
- `tests/OS.HostTests/NativeAppEntryStagingPreflightTests.cs`
  - Added success, payload-staging failure, default-readiness rejection,
    default-result rejection, and no-launch-boundary markers.
- `tests/OS.HostTests/BootSequenceAppEntryReadinessTests.cs`
  - Added boot-sequence rejection markers for failed and default staging
    preflight inputs.
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/cstorage.md`
  - Documented the host-tested staging preflight boundary and marker fields.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`
- `.\scripts\check-naming.ps1`

## Evidence

- `artifacts/logs/issue5-app-entry-staging-host-build-20260525.log`
  - Build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue5-app-entry-staging-hosttests-20260525.log`
  - Full host harness ended with `TEST_PASS host.tests`.
  - New markers include:
    - `TEST_PASS appmodel entry staging preflight accepts staged package and payload`
    - `TEST_PASS appmodel entry staging preflight rejects payload staging failure`
    - `TEST_PASS appmodel entry staging preflight rejects default staging readiness`
    - `TEST_PASS appmodel entry staging preflight default result is not success`
    - `TEST_PASS appmodel entry staging preflight preserves no-launch boundary`
    - `TEST_PASS boot.sequence app entry rejects staging preflight failure`
    - `TEST_PASS boot.sequence app entry blocks default staging preflight`
- `artifacts/logs/issue5-app-entry-staging-diffcheck-20260525.log`
  - `git diff --check` exited 0; output contains line-ending warnings only.
- `artifacts/logs/issue5-app-entry-staging-check-naming-20260525.log`
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`

## Failures

No final verification failures.

During iteration, an earlier default host-test build was blocked by a locked
`OS.HostTests.exe` process; the process exited and the default build/run passed.
A custom shared `OutputPath` host-test run was discarded because image-builder
tests expect the normal tool `bin\Debug\net10.0` layout.

## Deferred work

- Wire the app-entry staging preflight into the future default boot path only
  after the prerequisite storage and runtime phases have matching evidence.
- This step does not add live disk I/O, executable mapping, app launch, hosted
  .NET, Kestrel, QEMU boot proof, or cloud readiness.

## Evidence boundary

Host-tested contract evidence only. Package and payload staging are modeled from
caller-owned buffers and existing readiness metadata; no live storage, networking,
or diagnostics implementation was changed for this step.
