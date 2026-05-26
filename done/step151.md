# Step 151 - Native App Entry Readiness Boundary

## What Changed

- Added the final pre-entry native app readiness marker boundary to `BootSequence`.
- Added `APP_ENTRY_READY` and `APP_ENTRY_NOT_READY ...` marker formatting.
- Added host tests proving the app-entry boundary accepts a complete dependency chain, rejects dependency failures, and leaves the default boot marker plan unchanged.
- Updated QEMU native-app marker preset dry-run requirements to include `APP_ENTRY_READY`.
- Updated boot, boot-order, app-model, QEMU, image-builder, and completion-gate docs.

## Why It Changed

Gitea issue #168 needs a single ordered marker for the current package-to-invocation chain before a real app entry jump exists. This lets future QEMU/libvirt smoke paths require one final pre-entry marker without claiming that OS has allocated stacks, mapped pages, switched stacks, or executed app code.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequenceAppEntryReadinessTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `scripts/run-qemu-smoke.ps1`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/app-model.md`
- `docs/qemu.md`
- `docs/image-builder.md`
- `docs/completion-gates.md`
- `done/step151.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-step148-clean.log
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 errors. A parallel build attempt had one transient MSBuild cache warning, so this will be rechecked in the integrated pass.
- Host-test log emitted:
  - `TEST_PASS boot.sequence app entry ready`
  - `TEST_PASS boot.sequence app entry rejects dependency failure`
  - `TEST_PASS boot.sequence app entry preserves default markers`
- QEMU native-app dry-run emitted `TEST_PASS qemu.runner.dryrun` and included `APP_ENTRY_READY` in the required-marker list.

## Failures

- A first broad host-test run was interrupted because several concurrent worker host-test/ImageBuilder processes were stacked against the same output tree. It had already emitted the app-entry markers, but it is not counted as a full host-suite pass.

## Deferred Work

- No executable pages are allocated.
- No page protections are changed.
- No stacks are allocated or switched.
- No app ABI dispatch is implemented.
- No native app code is executed.
- The marker is not emitted by the live EFI artifact or default boot path.

## Evidence Boundary

This step is host-tested planning only. `APP_ENTRY_READY` proves only that the existing manifest/package/descriptor/payload-mapping/executable-memory/invocation status objects can be aggregated into one final pre-entry readiness marker.
