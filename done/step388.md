# Step 388 - Virtio-blk read pipeline composer

## Summary
Added a host-tested virtio-blk read pipeline composer that publishes one read, executes barrier-gated fake notify bytes, gates completion/reclaim on readiness, and projects the result into future `BlockReadResult` metadata.

## Why
The virtio-blk read path had separate host-tested contracts for publication, notify readiness, gated completion, and block-result projection. This step adds one orchestrating result so a future read-only block-device adapter can consume a single status-coded pipeline without claiming live hardware behavior.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step388.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS storage.virtioblk.readpipeline projects completed read`
  - `TEST_PASS storage.virtioblk.readpipeline rejects publication failure`
  - `TEST_PASS storage.virtioblk.readpipeline rejects readiness failure`
  - `TEST_PASS storage.virtioblk.readpipeline maps completion failure`
  - `TEST_PASS storage.virtioblk.readpipeline preserves no-live-device boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- This proves only host-tested virtio-blk read pipeline composition over caller-owned buffers: one read publication, barrier-gated fake notify byte execution, readiness-gated completion/reclaim, and projection into future `BlockReadResult` metadata.
- It does not map BARs, perform volatile/live MMIO, execute real memory barriers, allocate or map DMA memory, notify hardware, poll or wait for hardware, process interrupts, own a runtime descriptor allocator, copy payload bytes, perform block I/O, read sectors from a real disk, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, Kestrel, or production readiness.

