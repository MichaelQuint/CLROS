# Step 341 - Caller-Buffer GPT Scratch Diagnostics

## Summary

Added GPT scratch required/available byte diagnostics to the caller-buffer
manifest and package loader result paths.

## Why

The caller-buffer GPT ESP path already returned status codes when GPT header or
partition-entry scratch buffers were too small, but higher-level manifest and
package diagnostics did not expose the buffer sizes needed by a future
boot-owned scratch allocator. This slice makes those failures actionable while
staying in host-tested in-memory storage contracts.

## Files changed

- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `src/OS.Kernel/Manifest/NativeManifestLoader.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `docs/cstorage.md`
- `docs/manifest.md`
- `docs/completion-gates.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'gpt header capacity|gpt entry buffer capacity|TEST_PASS host.tests|TEST_FAIL'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Solution build passed with 0 warnings and 0 errors.
- Filtered host-test run emitted `TEST_PASS manifest loader caller-buffer reports gpt header capacity`.
- Filtered host-test run emitted `TEST_PASS manifest loader caller-buffer reports gpt entry buffer capacity`.
- Filtered host-test run emitted `TEST_PASS storage.package loader caller-buffer reports gpt header capacity`.
- Filtered host-test run emitted `TEST_PASS storage.package loader caller-buffer reports gpt entry buffer capacity`.
- Filtered host-test run emitted `TEST_PASS host.tests`.
- Full host-test run emitted the same four GPT scratch diagnostics markers and
  completed with `TEST_PASS host.tests`.
- `git diff --check` exited 0 with CRLF conversion warnings only.

## Failures

- During implementation, host tests initially failed because the manifest
  loader did not propagate nested GPT scratch metadata through `GptFail`; final
  focused verification passed after propagating the metadata.

## Deferred work

- Boot still does not own fixed scratch buffers for GPT header, GPT entry
  sectors, manifest bytes, or package bytes.
- The legacy `GptParser` entrypoints remain primary-only and still do not
  enforce GPT CRCs.
- No live virtio-blk, post-`ExitBootServices` disk I/O, boot-time package
  loading, app execution, hosted .NET, Minimal API, or Kestrel behavior is
  proven.

## Evidence boundary

This proves host-tested caller-buffer manifest/package loading reports GPT
header and partition-entry scratch size requirements over injected in-memory
block devices. It does not prove live storage hardware, QEMU boot-time package
loading, cloud-provider disk boot behavior, hosted .NET, Kestrel, or
production/cloud readiness.

