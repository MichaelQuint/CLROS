# Step 391 - App-selection boot scratch sizing

## Summary
Added a host-tested app-selection boot scratch plan that validates the caller-owned buffers needed before boot code can call `NativeAppSelectionLoader`.

## Why
The selection loader already composes caller-buffer GPT, manifest, package, launch-plan, and descriptor work, but future boot integration needs an explicit scratch sizing contract before any storage read occurs.

## Files changed
- `src/OS.Kernel/Manifest/NativeAppSelectionBootScratchPlan.cs`
- `tests/OS.HostTests/NativeAppSelectionBootScratchPlanTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step391.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS appmodel.selectionscratch accepts default image buffers`
  - `TEST_PASS appmodel.selectionscratch rejects short manifest buffer`
  - `TEST_PASS appmodel.selectionscratch rejects short package buffer`
  - `TEST_PASS appmodel.selectionscratch rejects short gpt entry buffer`
  - `TEST_PASS appmodel.selectionscratch rejects invalid requested sizes`
  - `TEST_PASS appmodel.selectionscratch preserves no-live-storage boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- Reserve or allocate the validated scratch buffers in the real boot path.
- Wire `NativeAppSelectionLoader` to a real block-device implementation once live storage can provide the manifest/package/descriptor prerequisites.

## Evidence boundary
This is host-tested sizing evidence only. It validates required and available capacity for caller-owned GPT header, GPT partition-entry-sector, partition-descriptor, manifest, and package buffers. It does not allocate boot memory, read live storage, parse manifests or packages, call the selection loader, map executable bytes, launch native apps, enter hosted .NET 10, start ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
