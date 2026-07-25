# Step 366 - PCI BAR sizing probe command plan

## Summary

Added a host-tested PCI BAR sizing probe planner that records the config-space
command sequence a future accessor must execute to write all ones, capture BAR
sizing readbacks, and restore original BAR values.

## Why

Step 365 added interpretation of caller-owned BAR sizing readback snapshots.
The next narrow prerequisite is the command plan that would produce those
snapshots once a live PCI config-space accessor exists. This keeps ordering and
64-bit BAR restore behavior explicit without executing any live config access.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingProbePlan.cs`
- `tests/OS.HostTests/PciBarSizingProbePlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step366.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.pci\.bar\.sizeprobe|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.pci.bar.sizeprobe plans 32bit mmio sequence
TEST_PASS drivers.pci.bar.sizeprobe plans 64bit mmio sequence
TEST_PASS drivers.pci.bar.sizeprobe skips absent io consumed bars
TEST_PASS drivers.pci.bar.sizeprobe rejects malformed descriptor
TEST_PASS drivers.pci.bar.sizeprobe rejects short command destination
TEST_PASS drivers.pci.bar.sizeprobe preserves no-live-config-access boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Implement the live PCI config-space accessor/executor for the planned BAR
  sizing write/read/restore commands.
- Feed executor-produced readbacks into `PciBarSizingPlanner` only after BAR
  restore safety, ECAM/config-space access rules, MMIO mapping/cacheability, and
  DMA ownership prerequisites are represented and tested.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested metadata planning of PCI BAR sizing probe commands
over already decoded type-0 BAR descriptors. It records the command sequence a
future PCI config-space accessor must execute to write all ones, capture sizing
readbacks, and restore original BAR values, including 64-bit low/high BAR pairs.
It does not execute live config-space writes, execute live config-space reads,
restore BARs on hardware, validate that a live device accepted the probe, map
BARs, perform MMIO, allocate or map DMA memory, handle interrupts, perform
packet I/O or block I/O, enumerate live PCI hardware, or prove cloud provider
readiness, hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel
readiness.
