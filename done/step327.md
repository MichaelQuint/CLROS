# Step 327 - Virtio-net TX Completion Planner

## Summary

Added `OS.Drivers.VirtioNetTransmitCompletionPlanner`, a host-tested caller-buffer planner that consumes successful TX descriptor publication plus successful TX queue-notification provenance, validates one split-ring used entry, and returns descriptor reclaim metadata.

## Why

The networking path needed a small completion boundary after queue notification before any live TX packet-I/O claim. This keeps completion handling status-coded and testable without polling hardware, processing interrupts, executing MMIO, mutating a descriptor allocator, or claiming that packets were transmitted.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitCompletionPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitCompletionPlanTests.cs`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step327.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`

## Evidence

- Focused host-test project build passed with 0 warnings and 0 errors.
- Full host harness passed and emitted:

```text
TEST_PASS drivers.virtionet.tx.completion accepts matching used entry
TEST_PASS drivers.virtionet.tx.completion advances wrapped used index
TEST_PASS drivers.virtionet.tx.completion reports no completion
TEST_PASS drivers.virtionet.tx.completion rejects unexpected used index
TEST_PASS drivers.virtionet.tx.completion rejects wrong descriptor head
TEST_PASS drivers.virtionet.tx.completion rejects unexpected byte count
TEST_PASS drivers.virtionet.tx.completion rejects short queue memory
TEST_PASS drivers.virtionet.tx.completion rejects failed publish
TEST_PASS drivers.virtionet.tx.completion rejects failed notification
TEST_PASS drivers.virtionet.tx.completion preserves no-mmio boundary
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.

## Failures

- Gitea issue updates remain blocked by missing write credentials in the current task context; API write attempts return `token is required`.
- No QEMU or live virtio-net evidence is claimed for this slice.

## Deferred work

- Execute queue setup and notification plans through a fake accessor with explicit memory-barrier preconditions.
- Add descriptor-free-list ownership that consumes TX completion reclaim metadata without reusing in-flight descriptors.
- Add live BAR-backed queue setup/notify/completion evidence before claiming real virtio-net TX packet I/O.
