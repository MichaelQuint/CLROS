# Step 339 - Backup GPT ESP Fallback

## Summary

Added backup GPT fallback to the caller-buffer GPT ESP location path used by
manifest and package loaders.

## Why

The caller-buffer GPT parser can validate backup headers and entry arrays, but
the manifest/package loaders still failed immediately when primary GPT location
failed. This slice lets those loaders recover from a bad primary GPT when a
valid backup GPT is present while preserving the original primary failure if
fallback also fails.

## Files changed

- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `docs/cstorage.md`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/completion-gates.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS storage.package loader caller-buffer gpt falls back to backup`.
- Host tests emitted `TEST_PASS manifest loader caller-buffer gpt falls back to backup`.
- Host tests emitted `TEST_PASS host.tests`.

## Failures

- None in the final verification run.

## Deferred work

- Legacy `GptParser` entrypoints remain primary-only and still do not validate GPT CRCs.
- Backup fallback is host-tested over in-memory block devices; boot does not yet own fixed scratch buffers for this path.
- No live virtio-blk, post-`ExitBootServices` disk I/O, or boot-time package loading is proven.

## Evidence boundary

This proves caller-buffer manifest/package GPT ESP location can recover from a
failed primary GPT by reading a valid backup GPT from an injected block device.
It does not prove live storage hardware, cloud-provider disk boot behavior,
hosted .NET, Kestrel, or production/cloud readiness.

