# Step 572 - Native manifest parse default-result guard

## Summary

Added explicit initialized-state gating to `NativeManifestParseResult` so a
default native manifest parse result no longer satisfies `Succeeded`.

## Why

The kernel-facing native manifest parser feeds package selection, launch-plan
creation, listen endpoint metadata, and future app readiness. Because the
success parse status is zero-valued, a default result could otherwise look like
a successful manifest parse before any manifest bytes had been validated.

## Files changed

- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step572.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `manifest kernel`, `TEST_PASS host.tests`, and `TEST_FAIL`.
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
TEST_START manifest kernel parses v0 native manifest
TEST_PASS manifest kernel parses v0 native manifest
TEST_START manifest kernel parses package app fields
TEST_PASS manifest kernel parses package app fields
TEST_START manifest kernel parses environment fields
TEST_PASS manifest kernel parses environment fields
TEST_START manifest kernel parses listen endpoint
TEST_PASS manifest kernel parses listen endpoint
TEST_START manifest kernel rejects invalid listen endpoint
TEST_PASS manifest kernel rejects invalid listen endpoint
TEST_START manifest kernel rejects duplicate environment keys
TEST_PASS manifest kernel rejects duplicate environment keys
TEST_START manifest kernel rejects invalid environment key
TEST_PASS manifest kernel rejects invalid environment key
TEST_START manifest kernel rejects too many environment fields
TEST_PASS manifest kernel rejects too many environment fields
TEST_START manifest kernel rejects unsupported runtime kind
TEST_PASS manifest kernel rejects unsupported runtime kind
TEST_START manifest kernel rejects missing app path
TEST_PASS manifest kernel rejects missing app path
TEST_START manifest kernel rejects malformed network mode
TEST_PASS manifest kernel rejects malformed network mode
TEST_START manifest kernel rejects unknown format
TEST_PASS manifest kernel rejects unknown format
TEST_START manifest kernel rejects unknown version
TEST_PASS manifest kernel rejects unknown version
TEST_START manifest kernel rejects duplicate keys
TEST_PASS manifest kernel rejects duplicate keys
TEST_START manifest kernel rejects oversized input
TEST_PASS manifest kernel rejects oversized input
TEST_START manifest kernel rejects non-ascii input
TEST_PASS manifest kernel rejects non-ascii input
TEST_START manifest kernel rejects control characters
TEST_PASS manifest kernel rejects control characters
TEST_START manifest kernel rejects unknown keys
TEST_PASS manifest kernel rejects unknown keys
TEST_START manifest kernel rejects malformed log mode
TEST_PASS manifest kernel rejects malformed log mode
TEST_START manifest kernel parse default result is not success
TEST_PASS manifest kernel parse default result is not success
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

- This is host-tested manifest parser metadata only. It does not prove
  boot-time manifest loading, package selection from live storage, app launch,
  executable mapping, native app execution, hosted .NET 10, Minimal API,
  Kestrel, cloud readiness, or production readiness.
