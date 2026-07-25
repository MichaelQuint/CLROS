# Step 303 - Guarded Physical-Memory Snapshot Boundary

## What Changed

- Added `IPlatformPhysicalMemory` and `IPlatformPhysicalMemorySource` as a guarded platform boundary for future firmware/physical-memory byte snapshots.
- Added `GuardedPlatformPhysicalMemory`, which denies reads after UEFI-gone simulation or `ExitBootServices`.
- Added status-coded result contracts for invalid physical ranges, empty or undersized destinations, unavailable sources, source faults, and short reads.
- Added host tests for the new guard and documented the boundary in boot, driver, and completion-gate docs.

## Why It Changed

ACPI table parsing is currently host-tested over caller-provided byte buffers. Before the live boot path can validate the firmware-provided RSDP and walk RSDT/XSDT/MCFG data, OS needs a narrow platform boundary for copying physical-address-backed bytes into caller-owned buffers without hiding UEFI-gone violations.

## Files Changed

- `src/OS.Kernel/Platform/IPlatformPhysicalMemory.cs`
- `src/OS.Kernel/Platform/GuardedPlatformPhysicalMemory.cs`
- `tests/OS.HostTests/PlatformPhysicalMemoryGuardTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step303.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies` passed with 0 errors after transient copy retries caused by a parallel host-test process.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors after the parallel process locks cleared.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- `git diff --check` exited 0. Git reported existing LF-to-CRLF normalization warnings only.

Focused markers observed:

```text
TEST_PASS platform.physical_memory read allowed before UEFI gone
TEST_PASS platform.physical_memory read denied in UEFI-gone simulation
TEST_PASS platform.physical_memory read denied after ExitBootServices
TEST_PASS platform.physical_memory read rejects invalid range
TEST_PASS platform.physical_memory read rejects empty destination
TEST_PASS platform.physical_memory read reports destination capacity
TEST_PASS platform.physical_memory read reports source failure
TEST_PASS platform.physical_memory read reports source unavailable
TEST_PASS platform.physical_memory read reports short read
TEST_PASS platform.physical_memory result contains no managed references
```

## Failures

- Initial solution build could not replace `tests/OS.HostTests/bin/Debug/net10.0/OS.HostTests.exe` because PID 6656 held it open. This was the same stale/parallel host-test lock pattern seen during earlier steps.
- A focused host-test project build later saw transient copy retries while PID 13624 held copied test dependencies, then succeeded after retries.
- A full host-test run exposed a #306 CI smoke test helper deadlock in redirected output handling. The #306 path was fixed and closed separately; the final full host-test run passed.

## Deferred Work

- No real physical memory is mapped or dereferenced.
- No ACPI RSDP, RSDT, XSDT, or MCFG table bytes are read from the live firmware address space.
- No cacheability, MMIO, paging, or post-`ExitBootServices` physical-memory access policy is implemented.
- The boundary is not wired into `EfiMain` or the live ACPI parser path.

## Evidence Boundary

This step is intended to prove only a host-tested, status-coded, UEFI-guarded byte-copy boundary over an injected physical-memory source. It is not live ACPI discovery, platform enumeration, MMIO, paging, or post-firmware execution evidence.
