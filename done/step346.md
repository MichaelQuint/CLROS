# Step 346 - TX completion requires executed notification provenance

## Summary

Changed virtio-net TX completion planning so it requires a successful
`VirtioNetQueueNotificationExecutionResult` from the fake-sink notification
executor instead of accepting planner-only notification metadata.

## Why

The TX completion/reclaim slice should not treat a calculated notify write as
equivalent to an executed notification boundary. Requiring the execution result
keeps completion gated on descriptor publication, explicit barrier-readiness
metadata, and fake-sink write success while still avoiding live MMIO claims.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitCompletionPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitCompletionPlanTests.cs`
- `tests/OS.HostTests/VirtioNetTransmitDescriptorOwnershipPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step346.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.tx\.completion|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build completed with 0 warnings and 0 errors.
- Filtered host run emitted:
  - `TEST_PASS drivers.virtionet.tx.completion accepts matching used entry`
  - `TEST_PASS drivers.virtionet.tx.completion accepts executed notification`
  - `TEST_PASS drivers.virtionet.tx.completion advances wrapped used index`
  - `TEST_PASS drivers.virtionet.tx.completion reports no completion`
  - `TEST_PASS drivers.virtionet.tx.completion rejects unexpected used index`
  - `TEST_PASS drivers.virtionet.tx.completion rejects wrong descriptor head`
  - `TEST_PASS drivers.virtionet.tx.completion rejects unexpected byte count`
  - `TEST_PASS drivers.virtionet.tx.completion rejects short queue memory`
  - `TEST_PASS drivers.virtionet.tx.completion rejects failed publish`
  - `TEST_PASS drivers.virtionet.tx.completion rejects failed notification`
  - `TEST_PASS drivers.virtionet.tx.completion rejects failed notification execution`
  - `TEST_PASS drivers.virtionet.tx.completion rejects wrong executed notification kind`
  - `TEST_PASS drivers.virtionet.tx.completion rejects executed notification queue mismatch`
  - `TEST_PASS drivers.virtionet.tx.completion rejects executed notification index mismatch`
  - `TEST_PASS drivers.virtionet.tx.completion preserves no-mmio boundary`
  - `TEST_PASS drivers.virtionet.tx.completion preserves executed-notification boundary`
  - `TEST_PASS host.tests`
- Full host run completed with `TEST_PASS host.tests` and included the TX
  completion markers above.
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` completed with exit code 0; Git reported existing CRLF
  normalization warnings only.

## Failures

- None in the commands listed above.

## Deferred work

- Replace the fake-sink notification executor with a host-tested virtio PCI
  register-accessor boundary before any live BAR/MMIO path.
- Add real memory-barrier execution only after the platform memory-ordering
  contract exists.
- Prove live TX notification and completion in QEMU only after BAR mapping,
  volatile register access, DMA ownership, and interrupt/polling rules exist.

## Evidence boundary

This is host-tested caller-owned TX completion planning evidence only. It
accepts a TX used-ring completion only after successful fake-sink
queue-notification execution provenance from `VirtioNetQueueNotificationExecutor`.
It does not prove hardware observed a notification, live MMIO, BAR mapping,
DMA allocation or mapping, interrupts, polling, packet transfer, networking
readiness, hosted .NET, Kestrel, cloud readiness, or production readiness.
