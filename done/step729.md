# Step 729 - Virtio-blk Queue-0 MMIO Setup Command Plan

## Summary

Added a host-tested virtio-blk queue setup command planner for queue 0. The planner consumes a successful block `VirtioDeviceDescriptor`, caller-provided queue size, physical split-ring addresses, and caller-provided queue metadata, then writes deterministic common-configuration command records for a future register-window executor.

## Why

The storage path already had read publication, notify metadata, and fake notify execution contracts, but it did not have a block-specific common-configuration queue setup contract equivalent to the virtio-net setup planning boundary. This slice adds the next no-live-MMIO step toward binding discovered virtio-blk descriptors to a future owned queue setup path without claiming a live driver.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockQueueMmioSetupPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockQueueMmioSetupPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `done/step729.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue695-host-build-rerun-20260525.log`: host test project build succeeded with 0 warnings and 0 errors.
- `artifacts\logs\issue695-hosttests-rerun-20260525.log`: host test run passed with:
  - `TEST_PASS storage.virtioblk.queuesetup plans queue zero common config commands`
  - `TEST_PASS storage.virtioblk.queuesetup rejects failed block descriptor`
  - `TEST_PASS storage.virtioblk.queuesetup rejects queue metadata`
  - `TEST_PASS storage.virtioblk.queuesetup rejects physical addresses`
  - `TEST_PASS storage.virtioblk.queuesetup reports command destination capacity`
  - `TEST_PASS storage.virtioblk.queuesetup default result is not success`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue695-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0 after docs and done-step updates; output contains only existing LF-to-CRLF working-copy warnings.

The first host test run for this slice failed because a valid network descriptor was classified as descriptor-not-ready before the block-kind check. The planner validation order was corrected so initialized non-block descriptors report `NotBlockDevice`, and the host tests passed on rerun.

## Failures

- No remaining test failure is known for this slice.
- The earlier failed host-test log remains at `artifacts\logs\issue695-hosttests-20260525.log` as pre-fix evidence only.

## Deferred work

- Map these planned command records to checked common-configuration register-window access metadata.
- Add fake register-window execution for the block setup command sequence.
- Feed live queue metadata from a real virtio-blk device once PCI/BAR/MMIO ownership is implemented.
- Add DMA-safe queue ownership, live queue setup, notification, completion waiting, and real block reads in later issues.

## Evidence boundary

This is host-tested command-planning evidence only. It does not perform live PCI enumeration, BAR mapping, register reads/writes, volatile MMIO, DMA allocation or mapping, descriptor publication to hardware, queue configuration, interrupts, block I/O, QEMU boot, libvirt boot, cloud-provider import, hosted .NET, ASP.NET Minimal API, or Kestrel work.

