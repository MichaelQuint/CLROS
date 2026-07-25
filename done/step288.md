# Step 288 - Caller-Owned MemoryMapInfo Region Writer

## What Changed

- Added a caller-owned `MemoryRegion` writer for normalized UEFI memory-map descriptors.
- Kept the existing allocation-based `MemoryMapInfoResult.FromUefiDescriptors(...)` API intact.
- Added `MemoryMapInfoRegionWriteResult` and `MemoryMapInfoRegionWriteStatus` for the no-allocation path.
- Reported writer status, descriptor count, region count, required/available region capacity, total pages/bytes, conventional-memory pages/bytes, and invalid descriptor index.
- Validated destination capacity before writing so destination-too-small failures do not partially modify caller storage.
- Added conventional-memory totals to the allocated `MemoryMapInfo` summary.
- Updated boot, architecture, and completion-gate docs to mark this as host-tested only until live EFI wiring exists.

## Why It Changed

The live EFI path currently owns raw descriptor bytes and normalizes only a compact
`EfiMemoryMapSummary`. Future allocator and early-kernel work needs a deterministic
way to materialize full `MemoryRegion` records into caller-provided storage before
general heap allocation is safe.

## Files Changed

- `src/OS.Kernel/Memory/MemoryMapInfo.cs`
- `src/OS.Kernel/Memory/MemoryMapInfoResult.cs`
- `tests/OS.HostTests/MemoryMapInfoTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/completion-gates.md`
- `done/step288.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
git diff --check
```

The host-test run was filtered in the terminal for:

```text
memory\.mapinfo writes caller-owned regions|memory\.mapinfo reports destination capacity|memory\.mapinfo rejects invalid caller-owned descriptors|TEST_FAIL|host\.tests
```

## Logs And Evidence

Focused project builds passed with 0 warnings and 0 errors.

The filtered host-test run emitted:

```text
TEST_PASS memory.mapinfo writes caller-owned regions
TEST_PASS memory.mapinfo reports destination capacity
TEST_PASS memory.mapinfo rejects invalid caller-owned descriptors
TEST_PASS host.tests
```

The full solution build passed with 0 warnings and 0 errors.

`git diff --check` exited 0. It printed existing LF-to-CRLF working-copy
warnings, but no whitespace errors.

## Failures

No failures were observed in this slice.

## Evidence Boundary

This is host-test evidence for descriptor-to-`MemoryRegion` materialization into
caller-provided storage. It does not prove live EFI wiring, full `MemoryMapInfo`
evidence from `EfiEntry`, `ExitBootServices`, allocator readiness, owned drivers,
storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

## Deferred Work

- Decode the live owned UEFI descriptor bytes into caller-owned descriptor storage.
- Invoke the caller-owned region writer from the live EFI or managed boot boundary.
- Use the resulting `MemoryRegion` records to design the first self-owned allocator
  only after post-firmware diagnostics and ownership rules are ready.
