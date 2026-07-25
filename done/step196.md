# Step 196 - Virtio-net transmit lifecycle

## What changed

- Added `VirtioNetTransmitLifecycle`, a host-testable TX publication contract for future virtio-net.
- Added host tests with `drivers.virtionet.tx.lifecycle` markers for successful publication and deterministic rejection paths.
- Updated `docs/drivers.md` to record the new host-tested TX lifecycle boundary and its evidence limits.

## Why it changed

The networking path needs a small, testable bridge between Ethernet frame construction and a future live virtio-net TX queue. This step keeps that bridge allocation-light and status-coded: callers provide the frame, TX header buffer, physical-address metadata, and split-ring memory, and the lifecycle writes only caller-owned memory.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitLifecycle.cs`
- `tests/OS.HostTests/VirtioNetTransmitLifecycleTests.cs`
- `docs/drivers.md`
- `done/step196.md`

## Tests run

```text
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Build completed with 0 warnings and 0 errors.

New host-test markers:

```text
TEST_PASS drivers.virtionet.tx.lifecycle writes header descriptors
TEST_PASS drivers.virtionet.tx.lifecycle writes available ring head
TEST_PASS drivers.virtionet.tx.lifecycle preserves unrelated memory
TEST_PASS drivers.virtionet.tx.lifecycle rejects failed queue plan
TEST_PASS drivers.virtionet.tx.lifecycle rejects header buffer
TEST_PASS drivers.virtionet.tx.lifecycle rejects frame bounds
TEST_PASS drivers.virtionet.tx.lifecycle rejects descriptor range
TEST_PASS drivers.virtionet.tx.lifecycle rejects queue memory and overflow
```

## Deferred work

- No live virtio-net device is configured.
- No DMA-safe memory allocation or physical mapping is performed.
- No MMIO status/queue register writes are performed.
- No device notification, interrupt handling, used-ring completion wait, or packet transmission is performed.
- RX buffer publication remains separate future work.
