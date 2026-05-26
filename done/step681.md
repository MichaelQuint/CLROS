# Step 681 - Virtio Package File Staging Diagnostic Formatter

## Summary

Added a caller-owned diagnostic formatter for the virtio-blk package-file
staging preflight boundary. The formatter emits a stable
`APP_PACKAGE_FILE_STAGING_PREFLIGHT ...` marker with nested read-session,
package-preflight, package-open, package-staging, byte-count, capacity, and
source-overflow metadata.

## Why

The boot-sequence readiness marker already reports high-level package-file
staging readiness. The next storage/app-model slices need a lower-level
diagnostic line that can be emitted before app launch work trusts staged
package bytes, without allocating or touching live storage.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.cs`
  - Added `VirtioBlockPackageFileStagingPreflightDiagnosticFormatter`.
  - Added status/result types with default-result rejection.
  - Added query-only required-byte reporting and short-destination rejection.
  - Formats initialized preflight results into caller-owned ASCII buffers.
- `tests/OS.HostTests/StorageVirtioBlockPackageReadPreflightTests.cs`
  - Added success-marker formatting coverage.
  - Added short-destination no-write coverage.
  - Added default/uninitialized formatter result rejection coverage.
- `docs/cstorage.md`
  - Documented the formatter contract and markers.
- `docs/completion-gates.md`
  - Preserved the Gate 7 storage evidence boundary for this marker.
- `done/step681.md`
  - Recorded this slice.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-integrated-602-605.log`

## Evidence

- Host-test build passed with 0 warnings and 0 errors.
- Integrated host-test run exited 0 and emitted `TEST_PASS host.tests`.
- Focused markers from the integrated run:
  - `TEST_PASS storage.virtioblk package file staging formatter formats success marker`
  - `TEST_PASS storage.virtioblk package file staging formatter reports short destination`
  - `TEST_PASS storage.virtioblk package file staging formatter default result is not success`

## Failures

- Initial formatter verification failed because query-only results reported
  `Success` status but did not satisfy the generic written-byte success guard.
  The formatter result now treats initialized query-only success as success when
  `RequiredBytes > 0` and `BytesWritten == 0`.
- The first expected marker string hard-coded an older package fixture byte
  count. The test now derives `packageBytes` from the package fixture.
- A separate host-test blocker in the Kestrel smoke fixture left
  `MinimalApiSmoke.dll` running and holding redirected logs open. That blocker
  was fixed separately in step 684.

## Deferred work

- Emit this marker from the future live boot package-loading path.
- Connect package-file staging evidence to executable mapping and app
  invocation only after live virtio-blk and boot-owned package reads exist.
- Add QEMU evidence after virtio-blk reads and package loading run in the guest.

## Evidence boundary

This is host-tested formatter and caller-owned package-file staging diagnostic
evidence only. It does not prove live virtio-blk, post-`ExitBootServices` disk
reads, boot-time package loading, executable mapping, app launch, hosted .NET
10, Kestrel, cloud readiness, or production readiness.
