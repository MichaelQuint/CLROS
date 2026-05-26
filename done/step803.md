# Step 803 - Virtio-blk Read Session Block Device Adapter

## Summary

Added a host-tested `IReadOnlyBlockDevice` adapter for completed full-disk
virtio-blk read-session destination bytes. The adapter validates read-session
completion, virtio block geometry, full-disk byte/block coverage, and caller
backing capacity before GPT/FAT/package loaders can consume the bytes through
the first-party block-device contract.

## Why

Issue #759 needed the existing caller-owned virtio-blk read-session boundary to
be consumable by storage loaders without claiming live virtio-blk I/O. This
step bridges completed bytes to `IReadOnlyBlockDevice` while preserving the
evidence boundary that no live MMIO, DMA, polling, interrupts, PAL calls, block
I/O, or real disk reads occur.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadSessionBlockDevice.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadSessionBlockDeviceTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step803.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: pass
- `git diff --check -- docs/cstorage.md docs/completion-gates.md`
  - Result: pass
  - Notes: log contains existing Git line-ending normalization warnings only.
- `git diff --no-index --check` against an empty temporary file for
  `src/OS.Kernel/Storage/VirtioBlockReadSessionBlockDevice.cs`,
  `tests/OS.HostTests/StorageVirtioBlockReadSessionBlockDeviceTests.cs`, and
  `done/step803.md`
  - Result: pass
  - Notes: used because these files are currently untracked; output contained
    existing Git line-ending normalization warnings only.

## Evidence

- `TEST_PASS done.step.allocate`
- `TEST_PASS storage.virtioblk.readsession block device reads completed destination`
- `TEST_PASS storage.virtioblk.readsession block device rejects default session`
- `TEST_PASS storage.virtioblk.readsession block device rejects incomplete session`
- `TEST_PASS storage.virtioblk.readsession block device rejects invalid geometry`
- `TEST_PASS storage.virtioblk.readsession block device rejects mismatched completed byte counts`
- `TEST_PASS storage.virtioblk.readsession block device rejects backing bytes too small`
- `TEST_PASS storage.virtioblk.readsession block device reports short read destination`
- `TEST_PASS storage.virtioblk.readsession block device reports out-of-range reads`
- `TEST_PASS storage.virtioblk.readsession block device default result is not success`
- `TEST_PASS host.tests`

## Failures

None in the final verification.

## Deferred work

This is host-tested completed-memory adaptation only. It does not prove live
PCI-backed virtio-blk, BAR mapping, live queue configuration, live device
notification, hardware completion waiting, interrupt handling, DMA-safe queue
ownership, boot-time package loading, app execution, hosted .NET, ASP.NET
Minimal API, Kestrel, cloud readiness, or production readiness.
