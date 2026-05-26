# Step 342 - Caller-Buffer GPT Source Diagnostics

## Summary

Added primary/backup GPT table-source metadata to caller-buffer GPT ESP
discovery and propagated it through package, manifest, and app-selection loader
results.

## Why

Backup-GPT fallback was already host-tested, but higher layers could only infer
which GPT table was used by inspecting overwritten scratch bytes. Future boot
diagnostics need the selected GPT source as explicit status metadata while the
storage path still uses caller-owned scratch buffers.

## Files changed

- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `src/OS.Kernel/Manifest/NativeManifestLoader.cs`
- `src/OS.Kernel/Manifest/NativeAppSelectionLoader.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `tests/OS.HostTests/NativeAppSelectionLoaderTests.cs`
- `docs/cstorage.md`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/completion-gates.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'reports primary gpt source|reports backup gpt source|TEST_PASS host.tests|TEST_FAIL'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Filtered host-test run emitted `TEST_PASS storage.package loader caller-buffer reports primary gpt source`.
- Filtered host-test run emitted `TEST_PASS storage.package loader caller-buffer reports backup gpt source`.
- Filtered host-test run emitted `TEST_PASS manifest loader caller-buffer reports primary gpt source`.
- Filtered host-test run emitted `TEST_PASS manifest loader caller-buffer reports backup gpt source`.
- Filtered host-test run emitted `TEST_PASS appmodel selection loader reports primary gpt source`.
- Filtered host-test run emitted `TEST_PASS appmodel selection loader reports backup gpt source`.
- Filtered host-test run emitted `TEST_PASS host.tests`.
- Full host-test run emitted the same six GPT source markers and completed
  with `TEST_PASS host.tests`.
- Solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with CRLF conversion warnings only.

## Failures

- None in focused verification.

## Deferred work

- Boot still does not own fixed scratch buffers for GPT header, GPT entry
  sectors, manifest bytes, or package bytes.
- The legacy `GptParser` entrypoints remain primary-only and still do not
  enforce GPT CRCs.
- No live virtio-blk, post-`ExitBootServices` disk I/O, boot-time package
  loading, app execution, hosted .NET, Minimal API, or Kestrel behavior is
  proven.

## Evidence boundary

This proves host-tested caller-buffer package, manifest, and app-selection
loaders report whether primary or backup GPT metadata was selected over
injected in-memory block devices. It does not prove live storage hardware, QEMU
boot-time package loading, cloud-provider disk boot behavior, hosted .NET,
Kestrel, or production/cloud readiness.
