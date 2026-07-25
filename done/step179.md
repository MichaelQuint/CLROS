# Step 179 - ACPI MCFG Discovery Composition

## Summary

Implemented issue #198 as a host-tested ACPI MCFG discovery composition layer. The new contract selects a checked `MCFG` table from caller-provided RSDT/XSDT table bytes or a direct pointer list, then parses that table into caller-owned PCI ECAM segment records.

## Why

The project needs a deterministic bridge from firmware table discovery metadata to the future PCI ECAM scanner without reading physical memory too early. This keeps ACPI/PCI progress testable while preserving the rule that live pointer dereference and ECAM mapping must wait for explicit platform memory rules.

## Files changed

- `src/OS.Kernel/Boot/AcpiMcfgDiscovery.cs`
- `tests/OS.HostTests/AcpiMcfgDiscoveryTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `done/step179.md`

## Tests run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

The solution build passed with 0 warnings and 0 errors.

The host test run emitted:

- `TEST_PASS acpi.mcfg.discovery selects and parses mcfg`
- `TEST_PASS acpi.mcfg.discovery preserves selected physical address`
- `TEST_PASS acpi.mcfg.discovery reports directory miss`
- `TEST_PASS acpi.mcfg.discovery reports mcfg parse failure`
- `TEST_PASS acpi.mcfg.discovery reports segment destination exhaustion`
- `TEST_PASS acpi.mcfg.discovery supports direct pointer list input`
- `TEST_PASS host.tests`

## Failures

No failures were observed in the #198 implementation path. A later full-solution re-run after other concurrent worker files appeared failed in `tests/OS.HostTests/VirtioPciCandidateSelectorTests.cs` with missing arguments to that file's `CreateDevice(...)` helper. That file is outside this issue's ownership and was not changed by this slice.

## Deferred work

- Live RSDP dereference and safe physical SDT snapshot reads.
- QEMU firmware MCFG discovery proof.
- ECAM MMIO mapping.
- Live PCI configuration-space reads and device enumeration.
- Driver binding, BAR mapping, interrupts, virtio feature negotiation, and queue ownership.
