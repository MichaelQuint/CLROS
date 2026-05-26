# Step 307 - ACPI RSDP Snapshot Validation Boundary

## What Changed

- Added `AcpiRsdpSnapshotReader`, which composes `IPlatformPhysicalMemory` with `AcpiRsdpValidator`.
- Added `AcpiRsdpSnapshotResult` and status codes for invalid physical addresses, missing physical-memory source, scratch-capacity failures, base/extended physical-read failures, and base/extended validation failures.
- Added host tests for ACPI 1.0 base reads, ACPI 2.0 extended-length re-reads, scratch sizing, physical-read failures, validation failures, and no-managed-reference result layout.
- Updated boot, driver, and completion-gate docs to describe the host-tested boundary and its evidence limit.

## Why It Changed

The project already had an ACPI RSDP pointer handoff and a byte validator, plus a guarded physical-memory read boundary. The missing piece was a narrow composition contract that reads RSDP bytes through the platform boundary before future RSDT/XSDT/MCFG table walking.

This keeps the next ACPI step testable without dereferencing live firmware memory in `EfiMain` or pretending that host-provided byte buffers are live platform discovery.

## Files Changed

- `src/OS.Kernel/Boot/AcpiRsdpSnapshot.cs`
- `tests/OS.HostTests/AcpiRsdpSnapshotTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step307.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies` initially failed because a parallel `OS.HostTests.exe` held the copied `OS.Kernel.dll`; the retry after the lock cleared passed with 0 warnings and 0 errors.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.

Focused markers observed:

```text
TEST_PASS acpi.rsdp.snapshot reads acpi1 base
TEST_PASS acpi.rsdp.snapshot rereads acpi2 extended length
TEST_PASS acpi.rsdp.snapshot reports scratch capacity
TEST_PASS acpi.rsdp.snapshot reports physical read failure
TEST_PASS acpi.rsdp.snapshot reports extended read failure
TEST_PASS acpi.rsdp.snapshot reports validation failure
TEST_PASS acpi.rsdp.snapshot result contains no managed references
```

## Failures

- The first focused host-test project build hit an existing parallel `OS.HostTests.exe` lock on `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`. No source change was required; the build passed after the lock cleared.

## Deferred Work

- No live firmware RSDP pointer is dereferenced.
- No RSDT or XSDT is read through physical memory.
- No MCFG, MADT, HPET, APIC, or PCI ECAM table is discovered from live firmware memory.
- No physical-memory mapping, cacheability policy, MMIO policy, paging policy, or post-`ExitBootServices` physical-memory access is implemented.
- The boundary is not wired into `EfiMain` or the ordered live boot path.

## Evidence Boundary

This step proves host-tested composition of the guarded physical-memory boundary with byte-level RSDP validation. It is not live ACPI discovery, real physical-memory mapping, platform enumeration, APIC/PCI bring-up, QEMU ACPI table walking, or cloud provider hardware evidence.
