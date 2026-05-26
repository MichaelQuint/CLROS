# Step 159 - PCI Type-0 Configuration Header Parser

## What Changed

- Added `PciConfigurationHeader` as a read-only parser for the first 64 bytes of PCI type-0 configuration space.
- Added status-coded parsing for vendor/device identity, revision/class/subclass/programming-interface metadata, raw header type, status register, BAR0-BAR5 values, and capability-list pointer metadata.
- Added explicit failure statuses for short buffers, vendor `0xFFFF`, unsupported header type, invalid capability-pointer alignment, and invalid capability-pointer range.
- Added host tests for required issue #178 marker coverage.
- Updated driver and completion-gate documentation with the parser-only evidence boundary.

## Why It Changed

Gitea issue #178 needs a small, deterministic PCI metadata boundary between ECAM address planning and later PCI enumeration. This parser lets future ECAM scanning hand a caller-owned 64-byte function snapshot to a status-coded C# contract before capability walking, BAR sizing, driver binding, or virtio discovery are wired to live hardware.

## Files Changed

- `src/OS.Kernel/Drivers/PciConfigurationHeader.cs`
- `tests/OS.HostTests/PciConfigurationHeaderTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step159.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue178.log
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors after a transient shared-compiler output lock was cleared with `dotnet build-server shutdown`.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `artifacts/logs/host-tests-issue178.log` exited with code 0 and emitted:
  - `TEST_PASS drivers.pci.header parses identity and class`
  - `TEST_PASS drivers.pci.header records bars and capability pointer`
  - `TEST_PASS drivers.pci.header rejects invalid vendor`
  - `TEST_PASS drivers.pci.header rejects invalid capability pointer`
  - `TEST_PASS host.tests`

## Failures

- The first standalone kernel build attempt failed because `VBCSCompiler` held `src/OS.Kernel/obj/Debug/net10.0/OS.Kernel.dll` while another build/test path was active. After `dotnet build-server shutdown`, the required kernel and host-test builds both passed cleanly.

## Deferred Work

- No ECAM scanning is implemented in this slice.
- No MMIO reads or writes are performed.
- No PCI capability walking is performed by this parser.
- No BAR sizing, BAR mapping, MSI/MSI-X setup, or driver binding is implemented.
- No virtio-blk or virtio-net device is discovered from live hardware.

## Evidence Boundary

This step is host-tested PCI configuration-header parsing only. It proves the first 64 bytes of a caller-provided type-0 PCI configuration-space snapshot can be decoded and validated with status-coded errors. It does not prove ACPI MCFG discovery, live ECAM reads, PCI enumeration, capability walking, BAR mapping, virtio device binding, storage, networking, or QEMU hardware evidence.
