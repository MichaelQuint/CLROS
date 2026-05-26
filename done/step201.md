# Step 201 - No-runtime CRC32 helper

## What Changed

- Added `OS.Std.Hash.Crc32`, a no-allocation reflected IEEE CRC32 helper for caller-owned spans.
- Exposed the reflected polynomial, initial state, and final XOR as constants so storage/network parsers can share one checked contract.
- Added host tests with `std.crc32` markers for empty input, the standard ASCII check vector, a binary vector, and split incremental updates.
- Updated the no-stdlib limits ledger to document the helper, tests, allocation behavior, and current performance boundary.

## Why It Changed

Storage work now has GPT and package-image paths that need CRC32 validation, but CRC behavior belongs in `std/no-runtime/shared` before kernel storage code consumes it. This slice adds the shared primitive without using lazy static reference fields, heap lookup tables, exceptions, or framework hashing APIs.

## Files Changed

- `std/no-runtime/shared/Hash/Crc32.cs`
- `tests/OS.HostTests/StdCrc32Tests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step201.md`

## Tests Run

```text
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Build completed with 0 warnings and 0 errors.

The integrated host-test harness emitted all new CRC32 markers before failing in unrelated parallel-owned virtio-net RX lifecycle work:

```text
TEST_PASS std.crc32 empty input
TEST_PASS std.crc32 ascii check vector
TEST_PASS std.crc32 binary vector
TEST_PASS std.crc32 incremental update
```

The captured log is `artifacts/logs/worker-f-crc32-hosttests.log`.

## Failures

`dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` exited nonzero after the CRC32 markers passed because `drivers.virtionet.rx.lifecycle reports completion failures` failed. That file is outside issue #222 and was not modified by this step.

## Deferred Work

- No storage parser has been migrated to validate GPT or package CRC fields yet.
- No table-driven fast path is implemented; this version intentionally favors freestanding initialization safety over throughput.
- No QEMU, firmware, or live disk evidence is provided by this host-only helper slice.

## Evidence Boundary

This step proves only deterministic CRC32 computation over host-testable `ReadOnlySpan<byte>` inputs. It is not evidence of GPT CRC enforcement, virtio-blk disk reads, package filesystem validation, native app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
