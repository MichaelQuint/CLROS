# Step 150 - Virtio PCI Discovery Contract

## What Changed

- Added `OS.Drivers.VirtioPciDeviceDiscovery` as a host-testable discovery contract for caller-provided PCI identity and decoded virtio capability metadata.
- The contract accepts virtio vendor `0x1AF4`, modern PCI device IDs `0x1040..0x107F`, maps virtio device ID `1` to network and `2` to block, and rejects unsupported vendors, legacy device IDs, and unsupported modern device kinds with explicit status codes.
- The contract records required common, notify, ISR, and device-configuration capability metadata and validates BAR, offset, and minimum length metadata without throwing from the kernel-facing path.
- Added host tests for virtio block recognition, virtio net recognition, unsupported device rejection, and required capability recording/rejection.
- Updated driver, storage, networking, and completion-gate documentation.

## Why It Changed

Gitea issue #166 needs the first virtio PCI discovery slice before live PCI enumeration, BAR mapping, feature negotiation, queue setup, or device I/O. This keeps storage and networking work aligned around one status-coded metadata contract without claiming hardware discovery.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioPciDeviceDiscovery.cs`
- `tests/OS.HostTests/VirtioPciDeviceDiscoveryTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step150.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\artifacts\focused-virtiopci\FocusedVirtioPciHarness.csproj --nologo
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- The temporary focused harness emitted:
  - `TEST_PASS drivers.virtiopci identifies virtio block device`
  - `TEST_PASS drivers.virtiopci identifies virtio net device`
  - `TEST_PASS drivers.virtiopci rejects unsupported device`
  - `TEST_PASS drivers.virtiopci records required capabilities`

## Failures

- None in the focused builds or focused harness.
- A broad `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String "drivers.virtiopci"` check exited 1 before emitting matching lines in the dirty shared test tree, so it is not used as evidence for this slice.

## Deferred Work

- No PCI ECAM scanner was added.
- No PCI capability-list walker was added.
- No BAR mapping was added.
- No virtio feature negotiation was added.
- No virtqueue publication, notification, interrupt handling, disk read, or packet I/O was added.
- No boot sequence or QEMU script changes were made.

## Evidence Boundary

This step is host-tested metadata planning only. `VirtioPciDeviceDiscovery` proves that already-decoded PCI identity and virtio capability metadata can be classified and validated for modern virtio block/network devices. It does not prove live PCI discovery, virtio-blk, virtio-net, storage, networking, or boot-time device ownership.
