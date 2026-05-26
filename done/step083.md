# Step 083 - MemoryExtensions LastIndexOf And CopyTo

## What Changed

- Added byte and byte-span `LastIndexOf` helpers to `OS.Std.MemoryExtensions`.
- Added BCL-shaped `CopyTo(ReadOnlySpan<byte>, Span<byte>)` coverage for exact, larger, overlap, and too-small destination scenarios.
- Updated host tests under the existing `std.memoryextensions byte span helpers` marker.

## Why It Changed

Issue #101 expands the shared no-runtime utility layer before storage and networking add more ad hoc byte-search or copy code.

## Files Changed

- `std/no-runtime/shared/MemoryExtensions.cs`
- `tests/OS.HostTests/StdMemoryExtensionsTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted `TEST_PASS std.memoryextensions byte span helpers`.
- The marker now covers byte/span `LastIndexOf`, exact/larger/overlap `CopyTo`, and too-small destination behavior.

## Failures

- No build or host-test failures after integration.

## Deferred Work

- `OS.Std.MemoryExtensions` remains a partial byte-only helper, not canonical `System.MemoryExtensions`.
- Kernel no-exception paths need a status-coded wrapper before relying on too-small `CopyTo` behavior.

## Evidence Boundary

This step proves host-tested shared utility behavior only. It is not a complete BCL surface.
