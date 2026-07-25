# Step 607 - Package File Staging Planner

## Summary

Added a host-tested `OsPackageFileStagingPlanner` storage contract for copying
an already-opened package file entry from caller-owned package bytes into
caller-owned staging storage.

## Why

Issue #5 still needs boot-time package/app loading, but live virtio-blk and app
execution are not ready. This slice advances the storage/app-model contract by
making the package-file staging boundary explicit without claiming live storage,
PAL file exports, executable page mapping, or app launch.

## Files changed

- `src/OS.Kernel/Storage/OsPackageFileStagingPlan.cs`
  - Added staging status/result values and the caller-owned staging planner.
- `tests/OS.HostTests/StoragePackageFileStagingPlanTests.cs`
  - Added host tests with stable `storage.package staging` markers.
- `docs/cstorage.md`
  - Documented the staging contract, markers, and evidence boundary.
- `docs/app-model.md`
  - Documented the app-model relevance and non-launch boundary.
- `docs/completion-gates.md`
  - Recorded the host-only gate evidence without changing gate status.
- `done/step607.md`
  - Recorded this step.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi /nodeReuse:false
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --artifacts-path C:\Projects\OS\artifacts\verify\step607\dotnet-artifacts /p:OSWorkloadProfile=SingleMinimalApi /nodeReuse:false
dotnet run --project .\artifacts\verify\step607\focused\StoragePackageFileStagingFocused.csproj --nologo
```

## Evidence

- Isolated host-test build with `--artifacts-path` completed with 0 warnings
  and 0 errors.
- Focused verifier emitted:
  - `TEST_PASS storage.package staging copies opened file`
  - `TEST_PASS storage.package staging rejects short destination`
  - `TEST_PASS storage.package staging rejects invalid package range`
  - `TEST_PASS storage.package staging rejects open failure`
  - `TEST_PASS storage.package staging preserves no-live-storage boundary`
  - `TEST_PASS storage.package staging default result is not success`
  - `TEST_PASS storage.package staging focused`
- A later full host-test run captured in
  `artifacts/logs/host-tests-step606.log` emitted the six
  `storage.package staging` markers above and `TEST_PASS host.tests`.

## Failures

- The standard host-test build path was blocked twice by concurrent
  `OS.HostTests.exe` processes holding files under
  `tests/OS.HostTests/bin/Debug/net10.0`.
- A first isolated build attempt used a relative `BaseIntermediateOutputPath`,
  which placed generated SDK assembly-info files under project directories and
  caused duplicate-attribute compile errors. Those generated
  `artifacts/verify/step607` directories under project folders were removed,
  and the successful retry used the SDK `--artifacts-path` root.
- Running the full isolated host harness from the alternate artifact path failed
  existing image-builder tests because those tests expect the normal tool output
  layout. The focused verifier was used for the new staging contract markers.
- A later normal-output full host-test run completed and emitted
  `TEST_PASS host.tests`.

## Deferred work

- No live virtio-blk read, boot-time package load, PAL file export, executable
  page mapping, relocation, app ABI dispatch, hosted .NET, or Kestrel behavior
  is implemented by this slice.
- A future boot path still needs boot-owned staging memory and a real
  disk-backed package source before this planner can feed app launch readiness.

