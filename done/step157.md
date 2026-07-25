# Step 157 - Virtio Block Read Transaction Planning Contract

## What Changed

- Added `VirtioBlockReadTransactionPlan` as a pure virtio-blk sector-read planning contract.
- Added status-coded validation for sector geometry, destination size, queue capacity, and descriptor requirements.
- Added focused host tests for valid read planning and failure paths.
- Preserved the issue #173 evidence after a concurrent done-step number collision.

## Why It Changed

Gitea issue #173 needs a bounded storage planning layer between generic virtio queue contracts and real virtio-blk I/O. This keeps read transaction sizing and descriptor requirements explicit before live queue publication, DMA allocation, interrupts, or disk reads exist.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockReadTransactionPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadTransactionPlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step157.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-integrated-171-176.log
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `artifacts/logs/host-tests-integrated-171-176.log` exited with `HOST_TEST_EXIT 0` and emitted:
  - `TEST_PASS storage.virtioblk read transaction plans sector range`
  - `TEST_PASS storage.virtioblk read transaction rejects destination too small`
  - `TEST_PASS storage.virtioblk read transaction rejects invalid sector geometry`
  - `TEST_PASS storage.virtioblk read transaction rejects queue too small`
  - `TEST_PASS host.tests`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `scripts/run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted `TEST_PASS qemu.runner.dryrun` and listed the native app marker requirements.
- `git diff --check` exited 0 with only existing LF-to-CRLF normalization warnings.

## Failures

- No implementation failures remain for this planning contract.
- The integrated host suite is slower than focused issue runs because it launches host tools through nested `dotnet run` calls.

## Deferred Work

- No live virtio-blk device initialization is implemented.
- No DMA-safe descriptor memory is allocated.
- No queue notify, interrupt, status-byte completion, or retry path is implemented.
- No disk sector is read from QEMU or libvirt in this slice.

## Evidence Boundary

This step is host-tested virtio-blk read transaction planning only. It proves deterministic descriptor/count/sector sizing and status-coded rejection for invalid plans. It does not prove live virtio-blk discovery, queue setup, DMA, interrupts, storage reads, filesystem access, manifest loading, or app launch.
