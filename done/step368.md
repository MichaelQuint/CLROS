# Step 368 - PCI BAR sizing probe fake execution

## Summary

Added a host-tested PCI BAR sizing probe executor that fake-executes
address-bound BAR sizing write/read/restore commands through an injected
config-accessor test double and captures caller-owned readback snapshots by BAR
index.

## Why

Step 367 bound the planned BAR sizing probe commands to checked ECAM
config-space addresses. The next narrow prerequisite is proving the command
sequence, readback capture, restore ordering, and accessor failure propagation
before any live PCI config-space accessor exists.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingProbeExecutionPlan.cs`
- `tests/OS.HostTests/PciBarSizingProbeExecutionPlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step368.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.pci\.bar\.sizeprobe\.fakeexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec captures 32bit readback
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec captures 64bit readbacks
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec rejects failed address plan
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec rejects short command source
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec propagates write failure
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec propagates read failure
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec preserves no-live-config-access boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Implement the live PCI config-space accessor/executor after post-firmware
  physical-memory/MMIO ownership and BAR restore safety are represented.
- Feed live executor-produced readbacks into `PciBarSizingPlanner` only after
  live config-space access, restore safety, ECAM mapping/cacheability, and DMA
  ownership prerequisites are tested.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested fake execution of address-bound PCI BAR sizing
probe commands through an injected config-accessor test double, with
caller-owned readback capture and restore-command ordering. It does not execute
live PCI config-space reads or writes, restore BARs on hardware, validate that a
live device accepted the probe, map ECAM or BARs, perform volatile MMIO,
allocate or map DMA memory, handle interrupts, enumerate live PCI hardware,
perform packet or block I/O, or prove cloud provider readiness, hosted .NET 10
readiness, ASP.NET Minimal API readiness, or Kestrel readiness.
