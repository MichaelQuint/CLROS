# Step 806 - Virtio-Block App Selection Read-Session Adapter

## Summary

Added a completed virtio-blk read-session entry point for native app selection
preflight. The new path creates a `VirtioBlockReadSessionBlockDevice` adapter
over caller-owned completed destination bytes and then delegates to the existing
`NativeAppSelectionLoader` path through `LoadSelection`.

## Why

The storage/app-model bridge already proved selected-app loading over an
injected `IReadOnlyBlockDevice`, but the future boot path needs a direct
preflight from the completed read-session memory produced by the virtio-blk
pipeline. This keeps the next slice bounded to completed-memory adaptation
instead of claiming live virtio-blk reads or app execution.

## Files changed

- `src/OS.Kernel/Manifest/VirtioBlockNativeAppSelectionPreflight.cs`
- `tests/OS.HostTests/VirtioBlockNativeAppSelectionPreflightTests.cs`
- `docs/cstorage.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step806.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass, 0 warnings, 0 errors.
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: pass, emitted `TEST_PASS host.tests`.
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo`
  - Result: pass, 0 warnings, 0 errors.
- Scoped `git diff --check` plus trailing-whitespace scan for the issue files.
  - Result: pass; Git reported LF/CRLF conversion warnings only.

## Evidence

- `OS.Kernel -> C:\Projects\OS\src\OS.Kernel\bin\Debug\net10.0\OS.Kernel.dll`
  appeared before the host-test project failed, proving the kernel-side
  preflight API compiled.
- `artifacts/logs/issue762-kernel-build.log` records the successful kernel
  build.
- `artifacts/logs/issue766-host-build.log` records the later successful
  combined host build after the unrelated test compile break was fixed.
- `artifacts/logs/issue766-host-tests.log` records the later successful full
  host-test run with `TEST_PASS host.tests`.
- `artifacts/logs/issue762-scoped-whitespace.log` records
  `TEST_PASS issue762.scoped_whitespace`.
- `artifacts/logs/issues762-766-diff-check.log` records
  `git-diff-check-exit=0` for the later combined evidence pass.
- `artifacts/logs/issues762-766-whitespace.log` records
  `TEST_PASS issues762_766.trailing_whitespace`.
- Verified host markers:
  - `TEST_PASS appmodel.virtio selection preflight adapts completed read session block device`
  - `TEST_PASS appmodel.virtio selection preflight reports adapter creation failure`

## Failures

- Initial host-test build failed with `CS0246: The type or namespace name
  'ISerialSink' could not be found` in
  `tests/OS.HostTests/NativeAppArgumentHandoffPlanTests.cs`. A later parent
  verification fixed that unrelated import and reran the host build/tests
  successfully.

## Deferred work

- Live virtio-blk PCI discovery, queue ownership, DMA, MMIO, real disk reads,
  boot-time package loading, executable mapping, app execution, hosted .NET,
  ASP.NET Minimal API, and Kestrel remain unproven.
