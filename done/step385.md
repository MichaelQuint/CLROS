# Step 385 - Virtio-blk read-notify readiness executor

## Summary
Added a host-tested virtio-blk read-notify readiness executor that composes read queue publication, notification metadata, checked notify-offset planning, and barrier-gated fake notify execution.

## Why
The existing virtio-blk pieces could publish a read request, derive notify metadata, plan a checked notify register-window write, and fake-execute that write after explicit barrier readiness. This step adds the next small composition boundary so later live-driver work has one status-coded result for "a successfully published read is ready to notify" without claiming live MMIO, hardware notification, completion, or disk I/O.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step385.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS storage.virtioblk.readnotify composes publication and barrier notify`
  - `TEST_PASS storage.virtioblk.readnotify rejects failed publication`
  - `TEST_PASS storage.virtioblk.readnotify rejects missing barrier`
  - `TEST_PASS storage.virtioblk.readnotify propagates notify execution failure`
  - `TEST_PASS storage.virtioblk.readnotify preserves no-live-device boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- Host-tested composition/fake notify only; not real hardware notification, disk I/O, cloud readiness, hosted .NET 10, ASP.NET Minimal API, Kestrel, or production readiness.
- Live virtio-blk still needs PCI enumeration, BAR mapping, feature negotiation, DMA-safe queue ownership, real device notification, hardware completion waiting, and integration with the package/app-loading path.

