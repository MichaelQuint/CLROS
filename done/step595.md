# Step 595 - Virtio app selection read-session destination coverage

## Summary

Added host-test coverage proving the virtio-blk app-selection preflight can
consume a full GPT/FAT/package image from the caller-owned read-session
destination buffer through the existing `IReadOnlyBlockDevice` and
`NativeAppSelectionLoader` path.

## Why

Issue #5 tracks storage/app-model progress toward reading packaged
manifest-selected apps through virtio-blk. Existing preflight tests preserved
read-session provenance and selected-app loader provenance, but they used a
separately supplied memory disk image. This slice tightens the host evidence by
using the simulated read-session destination bytes as the block-device source,
without claiming live virtio-blk, DMA, hardware completion, ExitBootServices, or
boot-time app loading.

## Files changed

- `tests/OS.HostTests/VirtioBlockNativeAppSelectionPreflightTests.cs`
- `docs/cstorage.md`
- `docs/app-model.md`
- `done/step595.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- Full host harness through a temp log outside the repo to avoid the
  source-firewall test scanning its own live output:
  `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Host-test project build passed with 0 warnings and 0 errors.
- Full host harness exited with code 0.
- Focused host-test output included:
  - `TEST_PASS appmodel.virtio selection preflight reads app from session destination`
  - `TEST_PASS host.tests`

## Failures

- No test failures in the commands above.
- Directly redirecting full host-test output into `artifacts/logs` caused the
  existing source-firewall module initializer to inspect a live repo-local log,
  so the successful full-host run wrote its temporary log outside the repo.

## Deferred work

- This is not live virtio-blk evidence. The test preloads caller-owned
  destination bytes to simulate device-written data before read-session
  completion parsing.
- No real PCI enumeration, DMA mapping, MMIO/BAR access, device notification,
  interrupt handling, hardware wait, ExitBootServices path, boot-owned scratch
  allocation, app execution, hosted .NET, or Kestrel behavior is implemented.
