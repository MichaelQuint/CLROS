# Step 041 - Raw UEFI Descriptor Reader

## What Changed

- Added `UefiMemoryDescriptorReader`, a status-coded decoder for raw UEFI memory descriptor bytes.
- Added explicit support for 40-byte descriptors and 48-byte descriptors with padding.
- Added validation for missing source buffers, invalid descriptor size/version/count, arithmetic overflow, source capacity, and destination capacity.
- Kept output allocation-free by requiring a caller-provided `Span<UefiMemoryDescriptor>`.
- Added host tests for successful 40-byte and 48-byte decode paths and validation failures.
- Updated boot, EFI build, architecture, audit, and completion-gate docs with the current evidence boundary.

## Why It Changed

The EFI path now owns a raw memory-map byte buffer. The next step toward `ExitBootServices` is a deterministic reader that can decode those bytes without relying on firmware-owned memory, allocation, exceptions, or ad hoc pointer casts.

## Files Changed

- `src/OS.Kernel/Boot/UefiMemoryDescriptorReader.cs`
- `tests/OS.HostTests/UefiMemoryDescriptorReaderTests.cs`
- `docs/boot.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `docs/architecture.md`
- `docs/repo-audit.md`
- `done/step041.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -p:UseAppHost=false -p:OutDir=C:\Projects\OS\artifacts\verify\hosttests\
dotnet .\artifacts\verify\hosttests\OS.HostTests.dll
```

## Evidence

- Build passed with 0 warnings and 0 errors in the isolated output directory.
- Host test marker: `TEST_PASS uefi.descriptor.reader decodes 40-byte stride`
- Host test marker: `TEST_PASS uefi.descriptor.reader decodes 48-byte stride`
- Host test marker: `TEST_PASS uefi.descriptor.reader rejects invalid metadata`
- Host test marker: `TEST_PASS uefi.descriptor.reader reports source and destination capacity`
- Full host test marker: `TEST_PASS host.tests`

## Failures

- The first normal host-test build attempt hit an existing lock from another running `OS.HostTests` process. The isolated output-directory build avoided the lock and passed.

## Deferred Work

- Wire `UefiMemoryDescriptorReader` into the live no-stdlib EFI handoff path.
- Convert the owned descriptor bytes into `MemoryMapInfo` before `ExitBootServices`.
- Preserve a map-key retry path for real `ExitBootServices`.

## Evidence Boundary

This step proves raw UEFI descriptor bytes can be decoded from caller-owned memory in host tests. It does not prove live EFI normalization, `ExitBootServices`, owned drivers, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
