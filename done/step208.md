# Step 208 - Caller-buffer GPT loader readiness

## What Changed

- Added additive caller-buffer GPT loader overloads for package and native manifest discovery.
- `OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers` now reads the primary GPT header and partition-entry sectors into caller-owned spans, uses `GptPartitionTableParser` for CRC-validating ESP selection, then continues through the existing FAT32 `OS/APP.PKG` and package parser path.
- `NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers` uses the same caller-buffer ESP readiness helper before reading `OS/MANIFEST.JSON` and parsing the native manifest.
- Package and manifest load results now preserve GPT table status, GPT CRC status, selected partition-entry index, required GPT entry bytes, and expected/computed GPT CRC32 metadata for diagnostics.

## Why

Issue #227 needed a safe migration path from the older heap-allocating, non-CRC-enforcing `GptParser` ESP path toward the caller-buffer GPT parser introduced by the earlier CRC work. The implementation is additive so existing loader behavior remains stable while the future boot path can move to fixed scratch buffers.

## Files Changed

- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `src/OS.Kernel/Manifest/NativeManifestLoader.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `docs/cstorage.md`
- `docs/manifest.md`

## Tests Run

```text
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

```text
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_PASS manifest loader caller-buffer gpt reads native manifest
TEST_PASS manifest loader caller-buffer gpt rejects crc mismatch
TEST_PASS storage.package loader caller-buffer gpt reads package
TEST_PASS storage.package loader caller-buffer gpt rejects crc mismatch
TEST_PASS host.tests
```

## Failures

No issue-local failures were observed in the build or integrated host harness.

## Deferred Work

- Existing `LoadFromPrimaryGptEsp` entrypoints still use the legacy `GptParser` for compatibility.
- The boot path does not yet own fixed GPT header, partition-entry, descriptor, manifest, and package scratch buffers.
- Backup GPT fallback is still unimplemented.
- This is still host/in-memory block-device evidence only; it is not virtio-blk, post-ExitBootServices disk, app launch, hosted .NET 10, Minimal API, or Kestrel evidence.
