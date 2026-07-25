# Step 390 - Virtio-blk read session wrapper

## Summary
Added a host-tested virtio-blk read session wrapper that carries caller-owned queue, cursor, descriptor-state, request, destination, status, and notify buffers across repeated adapter reads.

## Why
The adapter validates and executes one read pipeline call, but future boot/storage code also needs a small state boundary that preserves queue cursor and descriptor ownership across multiple reads without introducing a live block driver claim.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step390.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS storage.virtioblk.readsession delegates adapter read`
  - `TEST_PASS storage.virtioblk.readsession preserves cursor state`
  - `TEST_PASS storage.virtioblk.readsession rejects invalid setup`
  - `TEST_PASS storage.virtioblk.readsession preserves no-live-device boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- This proves only host-tested ref-struct session state over caller-owned buffers and adapter delegation with caller-prewritten completion snapshots.
- It is not an `IReadOnlyBlockDevice` implementation and does not map BARs, perform volatile/live MMIO, execute real memory barriers, allocate or map DMA memory, notify hardware, poll or wait for hardware, process interrupts, own a runtime descriptor allocator, copy payload bytes, perform block I/O, read sectors from a real disk, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, Kestrel, or production readiness.
