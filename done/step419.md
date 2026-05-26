# Step 419 - Virtio-blk package read preflight

## Summary

Added a host-tested virtio-blk package read preflight that bridges a
caller-owned read-session result into the existing package-loader input
contract.

## Why

Issue #404 needed a small adapter between virtio-blk read-session metadata and
package-loader buffers without claiming live disk reads. The preflight validates
that the read session completed, checks caller-owned GPT/FAT/package scratch
capacity against block-device geometry, preserves provenance, and only then
delegates to the existing package loader over an in-memory block device.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockPackageReadPreflight.cs`
- `tests/OS.HostTests/StorageVirtioBlockPackageReadPreflightTests.cs`
- `docs/cstorage.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step419.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test build passed with 0 warnings and 0 errors.
- Host test run ended with `TEST_PASS host.tests`.
- Full solution build passed with 0 warnings and 0 errors.
- Naming/source firewall emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0; Git reported CRLF normalization warnings only.
- New host markers:
  - `TEST_PASS storage.virtio package preflight validates buffers`
  - `TEST_PASS storage.virtio package preflight rejects incomplete session`
  - `TEST_PASS storage.virtio package preflight reports scratch capacity`
  - `TEST_PASS storage.virtio package preflight preserves storage boundary`

## Failures

- None.

## Deferred work

- This is host-tested preflight only. It does not execute live virtio-blk
  device notification, hardware waits, DMA mapping, BAR mapping, MMIO, real
  disk reads, boot-time package loading, app launch, hosted .NET, Kestrel,
  cloud-provider support, or production readiness.
