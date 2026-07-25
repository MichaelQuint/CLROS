# Step 035 - UEFI Memory Map Copy Plan

## What Changed

- Added `UefiMemoryMapCopyPlan`, `UefiMemoryMapCopyPlanStatus`, and `UefiMemoryMapCopyPlanResult`.
- Added status-coded validation for future copies from a firmware-owned UEFI memory-map buffer into OS-owned storage.
- Validated source buffer presence, map size, descriptor size/version, descriptor count, descriptor count bounds, destination capacity, and byte-count overflow.
- Added host tests covering valid metadata and failure cases.
- Updated boot and EFI build docs to describe the copy-plan boundary without claiming an EFI-side owned copy.

## Why It Changed

The live EFI path currently stores memory-map metadata and a firmware-owned buffer pointer. Before real `ExitBootServices`, the OS needs a tested contract for deciding whether that map can be copied into OS-owned storage. This step adds the validation model without mixing it with firmware calls or descriptor-byte copying.

## Files Changed

- `src/OS.Kernel/Boot/UefiMemoryMapCopyPlan.cs`
- `tests/OS.HostTests/UefiMemoryMapTests.cs`
- `docs/boot.md`
- `docs/efi-build.md`
- `done/step035.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- Focused `OS.Kernel` build passed with 0 warnings and 0 errors.
- Focused `OS.HostTests` build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS uefi.memorymap.copyplan validates metadata`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan rejects missing source`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan rejects invalid sizes`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan rejects invalid descriptor metadata`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan rejects descriptor count mismatch`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan rejects descriptor count limit`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan rejects overflow`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan rejects destination too small`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this copy-plan slice.

## Deferred Work

- Use the copy plan in `src/OS/Boot/EfiEntry.cs`.
- Copy descriptor bytes into an OS-owned static/bounded buffer.
- Emit a firmware-visible owned-map marker only after the copy succeeds.
- Decode raw owned descriptors into normalized memory-map structures.
- Add real `ExitBootServices` retry handling after owned diagnostics and drivers are ready.

## Evidence Boundary

This step proves metadata and capacity validation for a future memory-map copy. It does not copy UEFI descriptor bytes, does not make the live memory map OS-owned, does not call `ExitBootServices`, and does not claim owned drivers, allocator readiness, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
