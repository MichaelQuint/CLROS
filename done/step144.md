# Step 144 - Virtio-blk Read Request Layout Contract

## What Changed

- Added `OS.Storage.VirtioBlockRequestPlan` as the host-testable layout contract for the read-only virtio-blk milestone.
- Defined read/write request type values, virtio-blk status byte values, read-only feature bit metadata, and fixed request descriptor indexes.
- Added deterministic request metadata for:
  - 16-byte little-endian request header;
  - starting sector;
  - sector count;
  - 512-byte sector-size data length;
  - header descriptor read by device;
  - read data descriptor written by device;
  - status descriptor written by device.
- Added `VirtioBlockQueueReadiness.ValidateReadOnlyQueue` to require the read-only feature bit, a queue large enough for the three-descriptor chain, a valid request plan, and a read request.
- Rejected writes in read-only mode through explicit status codes.
- Added focused host-test source with the required issue #162 markers:
  - `TEST_PASS storage.virtioblk read request layout`
  - `TEST_PASS storage.virtioblk readiness accepts read-only queue`
  - `TEST_PASS storage.virtioblk readiness rejects missing features`
  - `TEST_PASS storage.virtioblk rejects write request in read-only mode`
- Updated storage, driver, and completion-gate docs with the host-test-only evidence boundary.

## Why It Changed

Gitea issue #162 needed a deterministic virtio-blk read request and queue-readiness contract before any live PCI/MMIO/DMA driver work. This gives future driver code a small, status-coded read-only layout target without performing MMIO, PCI discovery, DMA mapping, interrupts, queue notification, or disk reads.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockRequestPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockRequestPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step144.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --artifacts-path .\artifacts\dotnet-issue162
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- The default-output focused host-test project build failed because an existing `OS.HostTests (19440)` process held `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll` locked.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --artifacts-path .\artifacts\dotnet-issue162` passed with 0 warnings and 0 errors.

## Failures

- The first focused host-test project build could not copy `OS.Kernel.dll` into the default test output directory because another worker's `OS.HostTests` process held the file lock.
- Full host-test execution was not run, because it would execute the broad module-initializer suite rather than only issue #162 tests.

## Deferred Work

- No PCI discovery, BAR/MMIO access, feature negotiation with a live device, DMA-safe allocation, virtqueue descriptor publication, avail/used ring handling, queue notification, interrupt handling, real block reads, writes, flush, discard, or package-loader integration was added.
- The Gitea issue was not closed.

## Evidence Boundary

This step is a managed, compile-verified, host-test-sourced virtio-blk layout/readiness contract only. It proves the new kernel-facing source and test source compile, and it defines the focused #162 host-test markers. It does not prove full host-test execution in this pass, live virtio-blk I/O, post-ExitBootServices storage, package reads from a real disk, app loading, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
