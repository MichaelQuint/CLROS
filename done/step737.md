# Step 737 - Virtio-blk Read Session Setup Gate

## Summary
Added a host-tested virtio-blk read-session setup gate that requires queue setup register-window barrier execution evidence before the read session can publish descriptors or fake-notify a queue.

## Why
The storage stack had separate host evidence for queue setup barrier execution and for read-session publication/notification. This slice ties those contracts together so read-session tests cannot bypass the setup barrier and queue-enable precondition.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `done/step737.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence
- Build log: `artifacts/logs/issue703-host-build-20260525.log`
- Host-test log: `artifacts/logs/issue703-hosttests-20260525.log`
- Diff-check log: `artifacts/logs/issue703-git-diff-check-postdone-20260525.log`
- Expected markers:
  - `TEST_PASS storage.virtioblk.readsession setup gate composes setup and read`
  - `TEST_PASS storage.virtioblk.readsession setup gate rejects failed setup`
  - `TEST_PASS storage.virtioblk.readsession setup gate rejects missing barrier`
  - `TEST_PASS storage.virtioblk.readsession setup gate propagates read failure`
  - `TEST_PASS storage.virtioblk.readsession setup gate default result is not success`
  - `TEST_PASS host.tests`

## Failures
- Initial host-test run failed one new success-path assertion because the test expected the notify-window value to equal the queue notify offset. The virtio notify write value is the queue index, so the assertion was corrected before final evidence was collected.

## Deferred work
- Live virtio-blk BAR mapping, queue ownership, DMA-safe memory allocation, real memory barriers, hardware notification, completion waiting, interrupts, and real disk reads remain pending.
- This gate is not QEMU, `ExitBootServices`, cloud image, or app-execution evidence.

## Evidence boundary
- Host-tested sequencing contract only. The result proves that caller-owned read-session publication is gated on prior setup-barrier execution evidence in tests; it does not prove live hardware behavior or cloud readiness.

