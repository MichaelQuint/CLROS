# Step 389 - Virtio-blk read pipeline adapter preflight

## Summary
Added a host-tested virtio-blk read pipeline adapter that validates caller-owned request, destination, status, queue, descriptor-state, and notify-window buffers before delegating to the read pipeline composer.

## Why
The read pipeline composer can publish descriptors and advance caller-owned cursor state before notify-window execution fails. This adapter gives future storage integration a preflight boundary that rejects undersized caller-owned buffers before mutating queue, cursor, descriptor, request, status, or notify state.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step389.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS storage.virtioblk.readadapter executes pipeline read`
  - `TEST_PASS storage.virtioblk.readadapter rejects short request buffer`
  - `TEST_PASS storage.virtioblk.readadapter rejects short notify window`
  - `TEST_PASS storage.virtioblk.readadapter propagates pipeline failure`
  - `TEST_PASS storage.virtioblk.readadapter preserves no-live-device boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- This proves only host-tested adapter preflight over caller-owned buffers and delegation to the existing fake notify/completion pipeline.
- It does not map BARs, perform volatile/live MMIO, execute real memory barriers, allocate or map DMA memory, notify hardware, poll or wait for hardware, process interrupts, own a runtime descriptor allocator, copy payload bytes, perform block I/O, read sectors from a real disk, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, Kestrel, or production readiness.
