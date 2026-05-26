# Step 616 - App Package Staging Boot Marker Boundary

## Summary

Added a BootSequence readiness boundary for already-composed native app package
staging. The new host-tested path emits `APP_PACKAGE_STAGING_READY` only after
`NativeAppPackageStagingReadinessResult` succeeds, and emits a parseable
`APP_PACKAGE_STAGING_NOT_READY ...` marker with nested dependency and byte
metadata when staging readiness is blocked.

## Why

The storage/app-model chain already had host-tested planners for package-file
staging, payload staging, executable-memory planning, and invocation metadata.
Boot order still needed a single marker boundary that can be wired into a future
live boot path without reading storage, allocating staging buffers, mapping
pages, changing permissions, relocating code, or executing an app.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
  - Added `AppPackageStagingReadinessStatus`.
  - Added `APP_PACKAGE_STAGING_READY` and `APP_PACKAGE_STAGING_NOT_READY`
    marker constants.
  - Added `RunAppPackageStagingReadiness`.
  - Added `CreateAppPackageStagingNotReadyMarker`.
- `tests/OS.HostTests/BootSequenceAppPackageStagingReadinessTests.cs`
  - Added host tests for ready, failed, default-result, and unchanged default
    boot-marker behavior.
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/app-model.md`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe`
- `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host-test build completed with 0 warnings and 0 errors.
- Full host-test log: `artifacts/logs/host-tests-step616.log`.
- Machine-readable markers:
  - `TEST_PASS boot.sequence app package staging ready`
  - `TEST_PASS boot.sequence app package staging rejects readiness failure`
  - `TEST_PASS boot.sequence app package staging blocks default result`
  - `TEST_PASS boot.sequence app package staging preserves default markers`
  - `TEST_PASS host.tests`
- `dotnet build .\OS.slnx ...` completed with 0 warnings and 0 errors.
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` reported only existing line-ending warnings from Git's
  CRLF conversion; no whitespace errors were reported.

## Failures

- No compile, host-test, naming, source-firewall, or whitespace failures were
  observed for this slice.
- Claude MCP retry remained blocked by the local HTTP endpoint returning
  `500 Internal Server Error` for streamable-HTTP JSON-RPC requests; this was
  logged separately in Gitea and is not evidence for this kernel slice.

## Deferred work

- Wire app package staging readiness into the future live storage/app-launch
  boot path after virtio-blk, package loading, executable-memory ownership, and
  invocation handoff are real.
- Allocate boot-owned staging memory, map executable pages, enforce W^X/NX,
  perform relocation if needed, define the final native app ABI, and execute the
  selected app entrypoint.
- Add QEMU evidence only after the live boot path can reach these markers from
  real image contents.

## Evidence boundary

This step is managed host-test marker evidence only. It does not prove live
virtio-blk, boot-time package reads, manifest loading from disk, executable
mapping, page-permission changes, relocation, app launch, app stdout/stderr
routing, FetchApp, hosted .NET 10, Kestrel, or cloud readiness.
