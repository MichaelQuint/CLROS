# Step 367 - PCI BAR sizing probe address binding

## Summary

Added a host-tested PCI BAR sizing probe address planner that binds planned BAR
sizing write/read/restore commands to checked ECAM config-space address
metadata for a caller-provided PCI segment and bus/device/function.

## Why

Step 366 recorded the ordered command sequence for a future PCI config-space
accessor, but the commands still lacked concrete checked addresses. This step
adds the next metadata bridge before any live accessor can safely execute the
probe.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingProbePlan.cs`
- `src/OS.Kernel/Drivers/PciBarSizingProbeAddressPlan.cs`
- `tests/OS.HostTests/PciBarSizingProbeAddressPlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step367.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.pci\.bar\.sizeprobe\.address|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.pci.bar.sizeprobe.address binds 32bit command addresses
TEST_PASS drivers.pci.bar.sizeprobe.address binds 64bit command addresses
TEST_PASS drivers.pci.bar.sizeprobe.address propagates probe plan failure
TEST_PASS drivers.pci.bar.sizeprobe.address propagates ecam address failure
TEST_PASS drivers.pci.bar.sizeprobe.address rejects short destination
TEST_PASS drivers.pci.bar.sizeprobe.address preserves no-live-config-access boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Implement the live PCI config-space accessor/executor that consumes the
  address-bound BAR sizing write/read/restore commands.
- Feed executor-produced readbacks into `PciBarSizingPlanner` only after BAR
  restore safety, ECAM/config-space access rules, MMIO mapping/cacheability, and
  DMA ownership prerequisites are represented and tested.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested address-binding metadata for already planned PCI
BAR sizing probe commands. It binds write-all-ones, read-sizing-readback, and
restore-original commands to checked PCI config-space addresses for
caller-provided ECAM segment and bus/device/function metadata. It does not
execute live config-space writes or reads, restore BARs on hardware, validate
that a live device accepted the probe, map ECAM or BARs, perform volatile MMIO,
allocate or map DMA memory, handle interrupts, enumerate live PCI hardware,
perform packet or block I/O, or prove cloud provider readiness, hosted .NET 10
readiness, ASP.NET Minimal API readiness, or Kestrel readiness.
