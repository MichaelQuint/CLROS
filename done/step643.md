# Step 643 - App-selection scratch sector alignment guard

## Summary

Added a storage/app-model preflight guard so
`NativeAppSelectionBootScratchPlan` rejects sector sizes that are not aligned
to the storage/GPT minimum sector size before computing GPT partition-entry
scratch capacity.

## Why

The app-selection preflight path is meant to validate boot-owned GPT,
manifest, and package scratch buffers before any future storage read occurs.
It already rejected zero and oversized sector sizes, but it accepted values
such as 513 bytes even though `BlockDeviceInfo` and the caller-buffer GPT
parser require sector sizes aligned to the 512-byte minimum block contract.
Rejecting that shape early keeps app-selection diagnostics status-coded and
consistent with storage geometry rules.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppSelectionBootScratchPlan.cs`
  - Replaced the loose sector-size range check with the same minimum, maximum,
    and 512-byte alignment policy used by storage/GPT contracts.
- `tests/OS.HostTests/NativeAppSelectionBootScratchPlanTests.cs`
  - Added the machine-readable
    `appmodel.selectionscratch rejects unaligned sector size` host marker.
- `docs/app-model.md`
  - Documented that the scratch plan rejects unaligned sector sizes before
    GPT entry-sector capacity computation.
- `docs/cstorage.md`
  - Documented the storage/app-model preflight boundary for aligned scratch
    sector geometry.
- `done/step643.md`
  - Records this evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Build passed with 0 warnings and 0 errors.
- Full host-test run emitted:
  - `TEST_PASS appmodel.selectionscratch accepts default image buffers`
  - `TEST_PASS appmodel.selectionscratch rejects short manifest buffer`
  - `TEST_PASS appmodel.selectionscratch rejects short package buffer`
  - `TEST_PASS appmodel.selectionscratch rejects short gpt entry buffer`
  - `TEST_PASS appmodel.selectionscratch rejects invalid requested sizes`
  - `TEST_PASS appmodel.selectionscratch rejects unaligned sector size`
  - `TEST_PASS appmodel.selectionscratch default result is not success`
  - `TEST_PASS appmodel.selectionscratch preserves no-live-storage boundary`
  - `TEST_PASS host.tests`
- Logs:
  - `artifacts/logs/host-tests-step643-build.log`
  - `artifacts/logs/host-tests-step643.log`

## Failures

None in the focused build or full host-test run.

## Deferred work

This is host-tested scratch-preflight evidence only. It does not allocate
boot-owned scratch buffers, perform live virtio-blk I/O, parse guest storage at
boot, map executable memory, start a native app, enter hosted .NET, or prove
Kestrel/cloud readiness.
