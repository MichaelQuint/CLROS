# Step 423 - Virtio-block app-selection preflight boot markers

## Summary

Added a BootSequence readiness-marker bridge for the virtio-block native
app-selection preflight result. The new helper emits
`APP_SELECTION_PREFLIGHT_READY` only for an initialized successful preflight and
emits status-coded `APP_SELECTION_PREFLIGHT_NOT_READY` metadata for
default/uninitialized and failed preflight results.

## Why

`VirtioBlockNativeAppSelectionPreflight` now preserves read-session, scratch,
and selection-loader provenance, but `BootSequence` did not yet have a
preflight-specific marker boundary for that wrapper. This step keeps
`BootSequence.cs` as the boot-order marker authority while preserving the
evidence boundary: the default boot path is unchanged and no live virtio-blk
read, boot-time package load, app execution, hosted .NET, Kestrel, cloud, or
production readiness is claimed.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
  - Added `AppSelectionPreflightReadinessStatus`,
    `APP_SELECTION_PREFLIGHT_READY`,
    `APP_SELECTION_PREFLIGHT_NOT_READY`, and
    `RunAppSelectionPreflightReadiness`.
  - Added status-coded preflight marker formatting with initialized,
    read-session, pipeline, block-device, scratch, selection, manifest/package,
    launch, and descriptor metadata.
- `src/OS.Kernel/Manifest/VirtioBlockNativeAppSelectionPreflight.cs`
  - Exposed `IsInitialized` so `BootSequence` can distinguish default results.
- `tests/OS.HostTests/VirtioBlockNativeAppSelectionPreflightTests.cs`
  - Added BootSequence preflight success, default-result, and selection-failure
    marker coverage.
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/app-model.md`
- `docs/cstorage.md`
- `docs/completion-gates.md`
  - Documented the host-tested preflight marker boundary and evidence limits.
- `done/step423.md`
  - Recorded this milestone.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test build succeeded with 0 warnings and 0 errors.
- Full host test run completed with `TEST_PASS host.tests`.
- Full solution build succeeded with 0 warnings and 0 errors.
- Naming/source firewall succeeded:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- `git diff --check` exited 0; output contained only CRLF normalization
  warnings for already-edited text files.
- Added markers:
  - `TEST_PASS boot.sequence virtio app selection preflight ready`
  - `TEST_PASS boot.sequence virtio app selection preflight blocks default result`
  - `TEST_PASS boot.sequence virtio app selection preflight reports selection failure`
- Existing preflight markers continued passing:
  - `TEST_PASS appmodel.virtio selection preflight validates scratch`
  - `TEST_PASS appmodel.virtio selection preflight rejects incomplete session`
  - `TEST_PASS appmodel.virtio selection preflight preserves selection provenance`
  - `TEST_PASS appmodel.virtio selection preflight preserves no-launch boundary`
  - `TEST_PASS appmodel.virtio selection preflight default result is not success`

## Failures

The first host-test run during this slice failed once in
`qemu.runner.http_probe rejects body mismatch` with a QEMU smoke timeout. The
test passed on immediate rerun after the parallel worker finished, and the full
host-test run then completed with `TEST_PASS host.tests`.

## Deferred work

- Wire app-selection preflight into future live boot/storage sequencing only
  after live virtio-blk reads, boot-owned scratch allocation, and package-load
  ownership boundaries have evidence.
- Keep `APP_SELECTION_PREFLIGHT_READY` separate from `APP_SELECTION_READY`; the
  former is a preflight wrapper marker, not selected app execution readiness.

