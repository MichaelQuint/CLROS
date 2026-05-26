# Step 337 - Caller-Buffer Manifest And Package Size Diagnostics

## Summary
Implemented Gitea issue #322 by preserving required and available byte counts when caller-buffer manifest/package reads are too small, and by proving those diagnostics propagate through `NativeAppSelectionLoader` nested results.

## Why
The future boot path needs fixed, boot-owned scratch buffers for GPT, manifest, and package discovery. FAT32 already knows the `OS/MANIFEST.JSON` and `OS/APP.PKG` file sizes on destination-too-small failures, but the loader result shapes discarded that sizing detail before app selection could report it.

## Files changed
- `src/OS.Kernel/Manifest/NativeManifestLoader.cs`
- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `tests/OS.HostTests/NativeAppSelectionLoaderTests.cs`
- `docs/manifest.md`
- `docs/cstorage.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step337.md`

## Tests run
- `.\scripts\new-done-step.ps1 -Reserve`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence
- Solution build completed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS manifest loader caller-buffer reports manifest buffer capacity`.
- Host tests emitted `TEST_PASS storage.package loader caller-buffer reports package buffer capacity`.
- Host tests emitted `TEST_PASS appmodel selection loader reports manifest scratch capacity`.
- Host tests emitted `TEST_PASS appmodel selection loader reports package scratch capacity`.
- Full host harness emitted final `TEST_PASS host.tests`.

## Failures
- No #322 command failures.
- `git diff --check` reported only LF-to-CRLF warnings.

## Deferred work
- Caller-buffer GPT primary-header required-size reporting remains a separate small diagnostics slice.
- Backup GPT parsing remains pending storage parser hardening.
- This is host-only parser/loader evidence over in-memory block devices; it does not prove live virtio-blk, boot-time package reads, app execution, hosted .NET, Kestrel, cloud readiness, or production readiness.

