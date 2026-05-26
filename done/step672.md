# Step 672 - PAL Export Catalog, ASCII Token Splitter, And Package Staging Preflight

## Summary
Added three small implementation slices that advance the host-tested contracts
needed by PAL, no-runtime parsing, and storage/app-model handoff. The storage
slice now also has a `BootSequence` marker boundary for the package-file
staging preflight and preserves the caller-provided staging destination
capacity separately from the package-buffer capacity.

## Why
The CloudReady path needs narrow, status-coded contracts before live boot code
depends on them. The PAL side needed a required `OSHost_*` export catalog; the
no-runtime surface needed a reusable caller-owned token splitter for manifest
and protocol parsing; and storage needed a composed preflight from a completed
virtio-blk read-session package load through package file staging.

## Files changed
- `src/OS.Kernel/Pal/OSHostExportCatalog.cs`
- `std/no-runtime/shared/Text/AsciiTokenSplitter.cs`
- `src/OS.Kernel/Storage/VirtioBlockPackageFileStagingPreflight.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/PalExportCatalogTests.cs`
- `tests/OS.HostTests/StdAsciiTokenSplitterTests.cs`
- `tests/OS.HostTests/StorageVirtioBlockPackageReadPreflightTests.cs`
- `docs/pal.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/cstorage.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `.\scripts\build-efi-shell.ps1`
- `git diff --check`

## Evidence
- Host-test build passed with 0 warnings and 0 errors.
- Full host tests ended with `TEST_PASS host.tests`.
- New std markers include `TEST_PASS std.ascii tokens splits fields`,
  `TEST_PASS std.ascii tokens preserves empty fields`,
  `TEST_PASS std.ascii tokens reports destination capacity`,
  `TEST_PASS std.ascii tokens rejects invalid ranges`,
  `TEST_PASS std.ascii tokens rejects non ascii input`, and
  `TEST_PASS std.ascii tokens compatibility boundary`.
- New PAL markers include `TEST_PASS pal.export catalog lists required symbols`,
  `TEST_PASS pal.export catalog rejects duplicate symbols`,
  `TEST_PASS pal.export catalog rejects invalid names`, and
  `TEST_PASS pal.export catalog result default is not success`.
- New storage/app-model markers include
  `TEST_PASS storage.virtioblk package preflight stages package file` and
  `TEST_PASS boot.sequence package file staging preflight ready`,
  `TEST_PASS boot.sequence package file staging preflight blocks default result`,
  `TEST_PASS appmodel.selection preflight rejects incomplete read session`.
- The package-file staging preflight marker includes separate
  `destinationCapacity=<staging destination bytes>` and
  `packageBufferCapacity=<caller package buffer bytes>` metadata.
- `dotnet build .\OS.slnx --nologo -m:1`, `.\scripts\check-naming.ps1`,
  `.\scripts\run-qemu-smoke.ps1 -DryRun`, and `.\scripts\build-efi-shell.ps1`
  all passed after the final integrated changes.
- `git diff --check` returned exit code 0 with only LF-to-CRLF warnings.

## Failures
- None in the final verification. A worker saw a stale QEMU dry-run workload
  profile failure before the QEMU runner project was rebuilt; the integrated
  rebuild plus full host-test run passed afterward.

## Deferred work
- `OSHostExportCatalog` is a declaration/readiness catalog only. It does not
  export unmanaged symbols or make hosted CoreCLR/Kestrel runnable.
- `AsciiTokenSplitter` is an OS-specific ASCII byte-span helper. It does not
  allocate strings, decode UTF-8, trim values, quote/unescape fields, or parse
  nested manifest formats.
- `VirtioBlockPackageFileStagingPreflight` composes host-tested read-session,
  package-load, file-open, and staging contracts only. It does not perform live
  virtio-blk I/O, allocate boot-owned staging memory, map executable pages,
  launch native apps, start hosted .NET, or start Kestrel.

## Evidence Boundary
This step is host-tested contract evidence. It does not change any completion
gate to pass.
