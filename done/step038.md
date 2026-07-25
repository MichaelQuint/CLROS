# Step 038 - Platform Memory Map Guard Interface

## What Changed

- Added `IPlatformMemoryMap` as the first narrow platform memory-map interface.
- Added `PlatformMemoryMapResult`, `PlatformMemoryMapStatus`, and `PlatformMemoryMapOperations`.
- Modeled guarded memory-map snapshot copy as a status-coded operation that reports the attempted firmware operation and firmware-services mode.
- Added host tests for allowed pre-boundary snapshot copy, UEFI-gone simulation rejection, exited firmware-services rejection, and invalid snapshot status.
- Updated PAL docs with the memory-map guardrail and evidence boundary.

## Why It Changed

UEFI memory-map access needs to move behind a platform interface before real `ExitBootServices`. This step starts that encapsulation without wiring the no-stdlib EFI artifact to a full platform implementation and without claiming an OS-owned descriptor copy.

## Files Changed

- `src/OS.Kernel/Platform/IPlatformMemoryMap.cs`
- `tests/OS.HostTests/PlatformMemoryMapGuardTests.cs`
- `docs/pal.md`
- `done/step038.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Host test marker: `TEST_PASS platform.memorymap allows snapshot copy before UEFI gone`
- Host test marker: `TEST_PASS platform.memorymap rejects snapshot copy in UEFI-gone simulation`
- Host test marker: `TEST_PASS platform.memorymap rejects snapshot copy after ExitBootServices`
- Host test marker: `TEST_PASS platform.memorymap reports invalid snapshot as status`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this platform-interface slice after main-thread verification.

## Deferred Work

- Implement a real UEFI-backed `IPlatformMemoryMap`.
- Copy descriptor bytes into OS-owned storage before `ExitBootServices`.
- Add remaining platform interfaces for console, filesystem, keyboard, timer, and optional RNG.
- Wire platform interfaces into the no-stdlib EFI boundary.

## Evidence Boundary

This step proves host-tested guard behavior for a platform memory-map interface. It does not prove live UEFI descriptor copying, OS-owned memory-map storage, `ExitBootServices`, owned drivers, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
