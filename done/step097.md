# Step 097 - Kernel Native Manifest Parser

## What Changed

- Added `NativeManifestParser` for a deterministic kernel-facing v0 native manifest format.
- Added readonly manifest field/result structs that reference offsets into caller-provided manifest bytes.
- Added status-coded rejection for missing fields, malformed modes, unknown format/version, duplicate keys, oversized input, non-ASCII/control bytes, unknown keys, and malformed log modes.

## Why It Changed

Issue #112 adds the kernel-side parser boundary needed before storage-loaded manifests can drive native app launch. It avoids depending on host JSON parsing or reflection in kernel-facing code.

## Files Changed

- `src/OS.Kernel/Manifest/NativeManifest.cs`
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `docs/manifest.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS manifest kernel parses v0 native manifest`
  - `TEST_PASS manifest kernel rejects missing app path`
  - `TEST_PASS manifest kernel rejects malformed network mode`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No JSON-to-native manifest conversion.
- No manifest read from disk.
- No app launcher integration.
- No hosted .NET 10 or Kestrel launch behavior.

## Evidence Boundary

This step proves host-side kernel manifest parsing only. It is not manifest loading, app launch, hosted .NET 10, or Kestrel evidence.
