# Step 172 - ACPI Table Directory Selector

## What Changed

- Added `AcpiTableDirectory`, a host-testable ACPI table selector that bridges parsed `RSDT`/`XSDT` physical-address lists to caller-provided SDT byte snapshots.
- Added result/status metadata for selected table physical address, pointer index, source index, requested signature, table signature, declared length, and capacity failures.
- Added host tests for selecting `MCFG`, deterministic first-match behavior, missing table sources, invalid checksums, destination exhaustion, and signature misses.
- Updated boot and driver docs to describe the new host-test-only ACPI directory layer.

## Why It Changed

Issue #191 needed the next ACPI discovery step after `AcpiSdtParser`. The OS can now validate caller-owned SDT byte snapshots and select a requested table such as `MCFG` without adding unsafe physical-memory reads or claiming QEMU firmware table walking.

## Files Changed

- `src/OS.Kernel/Boot/AcpiTableDirectory.cs`
- `tests/OS.HostTests/AcpiTableDirectoryTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `done/step172.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and emitted `TEST_PASS host.tests`.
- New ACPI directory markers emitted:
  - `TEST_PASS acpi.directory selects mcfg from xsdt pointer list`
  - `TEST_PASS acpi.directory selects first matching signature deterministically`
  - `TEST_PASS acpi.directory rejects missing table source`
  - `TEST_PASS acpi.directory rejects invalid table checksum`
  - `TEST_PASS acpi.directory reports destination capacity exhaustion`
  - `TEST_PASS acpi.directory reports signature not found`

## Deferred Work

- No physical-memory reads were added.
- The live EFI path still does not dereference the firmware RSDP pointer, parse real `RSDT`/`XSDT` bytes, or select real firmware `MCFG`.
- QEMU/libvirt ACPI table discovery evidence remains pending until a controlled physical-memory snapshot path exists.
