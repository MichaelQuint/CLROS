# Step 165 - Virtio-blk Descriptor Chain Writer

## What Changed

- Added `VirtioBlockDescriptorChainWriter` to materialize a planned virtio-blk descriptor chain into caller-owned split-ring memory.
- Added host tests for descriptor bytes, available-ring publication fields, unrelated memory preservation, and deterministic rejection statuses.
- Updated storage, driver, and completion-gate documentation with the new caller-owned-memory evidence boundary.

## Why

Gitea issue #190 tracks the next virtio-blk slice after descriptor-chain planning: writing the planned descriptor table entries and available-ring fields into a supplied queue memory span without taking ownership of DMA allocation, MMIO, device notification, completion handling, or sector reads.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockDescriptorChainWriter.cs`
- `tests/OS.HostTests/StorageVirtioBlockDescriptorChainWriterTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step165.md`

## Implemented Behavior

- `VirtioBlockDescriptorChainWriter.Write(...)` accepts a descriptor-chain plan result, a split-ring plan result, and caller-owned queue memory.
- Successful writes materialize exactly three 16-byte virtqueue descriptor entries in little-endian format: address, length, flags, and next.
- Successful writes store the planned head descriptor into the planned available-ring slot and advance `avail.idx`.
- Validation runs before writes so failed plans and invalid queue spans leave caller memory untouched.
- Deterministic failure statuses cover failed descriptor plans, failed queue plans, descriptor indexes outside the supplied queue size, invalid available-ring slots, queue-plan mismatch, undersized queue memory, and arithmetic overflow.
- The writer explicitly does not allocate DMA memory, write MMIO registers, notify a device, wait for used-ring completion, or read sectors.

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -p:OutputPath=C:\Projects\OS\artifacts\verify-bin\hosttests\
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue190.log
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors after pre-existing host-test processes released `OS.HostTests.exe`.
- Isolated host-test project build passed with 0 warnings and 0 errors.
- `artifacts/logs/host-tests-issue190.log` emitted `TEST_PASS host.tests`.
- Descriptor-writer host-test markers emitted:
  - `TEST_PASS storage.virtioblk descriptor writer writes three descriptors`
  - `TEST_PASS storage.virtioblk descriptor writer writes available ring head`
  - `TEST_PASS storage.virtioblk descriptor writer preserves unrelated memory`
  - `TEST_PASS storage.virtioblk descriptor writer rejects failed plan`
  - `TEST_PASS storage.virtioblk descriptor writer rejects undersized queue memory`
  - `TEST_PASS storage.virtioblk descriptor writer rejects descriptor index range`
  - `TEST_PASS storage.virtioblk descriptor writer rejects invalid available slot`
  - `TEST_PASS storage.virtioblk descriptor writer rejects arithmetic overflow`

## Failures

- The first normal solution build failed because `OS.HostTests.exe` was locked by already-running `OS.HostTests` processes `6884` and `10004`.
- A full host-test run from the nonstandard isolated output path failed in `ImageBuilderTests` because those tests derive the tool path from the normal repository output layout. The standard `dotnet run --project ... --no-build` host-test run passed after the normal build succeeded.

## Deferred Work

- Wire the writer to live virtio-blk queue ownership only after feature negotiation, DMA-safe queue memory ownership, PCI/BAR mapping, device notification, interrupt/completion handling, and read-only disk I/O rules are represented by tests.
- Keep the first live driver slice read-only.

## Evidence Boundary

This step is host-tested caller-owned-memory evidence only. It proves planned descriptor bytes and available-ring fields can be written into a supplied split-ring memory span. It does not prove PCI enumeration, BAR mapping, DMA-safe allocation, live virtqueue publication, MMIO queue notification, interrupt handling, used-ring completion, post-ExitBootServices storage, real disk reads, package loading from real storage, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
