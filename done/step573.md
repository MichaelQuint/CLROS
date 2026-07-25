# Step 573 - Native manifest loader default-result guard

## Summary

Added explicit initialized-state gating to `NativeManifestLoadResult` so a
default native manifest loader result no longer satisfies `Succeeded`.

## Why

The native manifest loader records GPT, partition, FAT32, manifest parse, and
caller-buffer provenance before future app selection. Because the loader success
status is zero-valued, a default result could otherwise look like a successful
manifest load before any disk image, FAT32 file, or manifest bytes had been
validated.

## Files changed

- `src/OS.Kernel/Manifest/NativeManifestLoader.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step573.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `manifest loader`, `TEST_PASS host.tests`, and `TEST_FAIL`.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START manifest loader reads native manifest from gpt esp
TEST_PASS manifest loader reads native manifest from gpt esp
TEST_START manifest loader caller-buffer gpt reads native manifest
TEST_PASS manifest loader caller-buffer gpt reads native manifest
TEST_START manifest loader caller-buffer reports primary gpt source
TEST_PASS manifest loader caller-buffer reports primary gpt source
TEST_START manifest loader caller-buffer gpt falls back to backup
TEST_PASS manifest loader caller-buffer gpt falls back to backup
TEST_START manifest loader caller-buffer reports backup gpt source
TEST_PASS manifest loader caller-buffer reports backup gpt source
TEST_START manifest loader caller-buffer gpt rejects crc mismatch
TEST_PASS manifest loader caller-buffer gpt rejects crc mismatch
TEST_START manifest loader caller-buffer reports gpt header capacity
TEST_PASS manifest loader caller-buffer reports gpt header capacity
TEST_START manifest loader caller-buffer reports gpt entry buffer capacity
TEST_PASS manifest loader caller-buffer reports gpt entry buffer capacity
TEST_START manifest loader caller-buffer reports manifest buffer capacity
TEST_PASS manifest loader caller-buffer reports manifest buffer capacity
TEST_START manifest loader rejects missing esp
TEST_PASS manifest loader rejects missing esp
TEST_START manifest loader rejects malformed native manifest
TEST_PASS manifest loader rejects malformed native manifest
TEST_START manifest loader default result is not success
TEST_PASS manifest loader default result is not success
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- None.

## Deferred work

- This is host-tested manifest loader metadata only. It does not prove live
  virtio-blk reads, boot-time manifest or package selection, app launch,
  executable mapping, native app execution, hosted .NET 10, Minimal API,
  Kestrel, cloud readiness, or production readiness.
