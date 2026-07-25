# Step 026 - std/no-runtime Endian Primitives

## What Changed

- Added `OS.Std.Binary.EndianPrimitives` under `std/no-runtime/shared`.
- Implemented allocation-free span-based read/write helpers for `UInt16`, `UInt32`, and `UInt64` in little-endian and big-endian forms.
- Added non-throwing `TryRead` and `TryWrite` variants for parser-facing kernel code.
- Added host tests for exact byte order, too-short buffers, no-write short-buffer behavior, and round trips.
- Updated storage and networking docs so future parsers use this shared helper before adding local byte-shifting code.

## Why It Changed

Storage, packet, virtio, DNS, TCP, and package metadata parsers all need fixed-width endian conversion. Putting that behavior in `std/no-runtime/shared` first prevents each subsystem from creating incompatible ad hoc parsing helpers.

## Files Changed

- `std/no-runtime/shared/Binary/EndianPrimitives.cs`
- `tests/OS.HostTests/StdEndianPrimitivesTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/cstorage.md`
- `docs/networking.md`
- `done/step026.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and emitted `TEST_PASS host.tests`.
- The host test run emitted `TEST_PASS std.endian primitives`.

## Failures

No failures remain for this slice.

## Deferred Work

- Migrate existing network packet helpers and future storage parsers to `OS.Std.Binary.EndianPrimitives`.
- Add signed integer helpers only when a real parser needs them.
- Keep this type in `OS.Std.Binary`, not `System.Buffers.Binary`, until compatibility is explicitly documented.

## Evidence Boundary

This step proves host-tested endian helper behavior in the shared no-runtime project. It does not prove storage, virtio, networking, TCP, or package parsing behavior.
