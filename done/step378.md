# Step 378 - Virtio-blk read queue completion planner

## Summary

Added a host-tested virtio-blk read queue completion planner that consumes a
successful read queue publication result, parses one caller-owned completion,
reclaims descriptor ownership, and advances the used-ring cursor after
successful reclaim.

## Why

Step 377 added the range-validated read queue publication path. This slice adds
the matching completion-side contract so later live virtio-blk work has a
deterministic host-tested publish/complete pair before hardware notification and
completion waiting exist.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step378.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.readqueue completion|storage\.virtioblk\.readqueue completes|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.readqueue completes and advances used index
TEST_PASS storage.virtioblk.readqueue completion rejects failed publication
TEST_PASS storage.virtioblk.readqueue completion rejects no completion
TEST_PASS storage.virtioblk.readqueue completion preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Compose the read queue publish/complete pair with future live virtio-blk
  notification, readiness, and completion waiting once BAR-backed register
  access, memory ordering, and DMA-safe queue ownership are defined.
- Add a real runtime queue allocator and descriptor ownership model before
  multiple outstanding block requests are allowed.
- Do not treat this as live virtio-blk I/O, cloud readiness, hosted .NET 10
  readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested range-validated completion/reclaim over
caller-owned virtio-blk queue memory, status buffer, descriptor state, and cursor
state. It rejects failed publications before reading queue memory and advances
the caller-owned used index only after successful completion/reclaim. It does
not allocate DMA-safe memory, map buffers for a live device, publish
descriptors to a live device-owned virtqueue, execute MMIO, notify hardware,
process interrupts, poll or wait for hardware, own a full runtime descriptor
allocator, read sectors, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal
API, or Kestrel readiness.
