# Step 760 - Manifest Environment Formatter Shared ASCII Helpers

## Summary

Migrated the native manifest environment diagnostic formatter from
manifest-local decimal counting/writing and byte-copy loops to the shared
`OS.Std.Text` no-runtime ASCII helpers.

## Why

`NATIVE_MANIFEST_ENV` is a kernel-facing app-model diagnostic marker. Its
required-byte accounting and caller-owned write path should use the same shared
ASCII formatting helpers as the other migrated diagnostics, keeping local
integer-to-ASCII loops out of manifest code.

## Files changed

- `src/OS.Kernel/Manifest/NativeManifestEnvironmentDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `docs/manifest.md`
- `done/step760.md`
- `artifacts/logs/issue719-host-build-20260525.log`
- `artifacts/logs/issue719-hosttests-20260525.log`
- `artifacts/logs/issue719-git-diff-check-20260525.log`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue719-manifest-env-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue719-manifest-env-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- `artifacts/logs/issue719-host-build-20260525.log`: build succeeded with 0
  warnings and 0 errors.
- `artifacts/logs/issue719-hosttests-20260525.log`: emitted
  `TEST_PASS manifest env formatter uses shared ascii text builder`,
  preserved the existing manifest environment diagnostic/readiness markers, and
  ended with `TEST_PASS host.tests`.
- `artifacts/logs/issue719-git-diff-check-20260525.log`: `git diff --check`
  exited successfully. The log contains Git LF/CRLF working-copy warnings from
  the dirty tree and no whitespace-error failure.

## Failures

- None. No Gitea issue was closed or updated.

## Deferred work

- This is host-tested diagnostic-formatting evidence only. It does not
  materialize process environment variables, read manifests from live storage,
  wire the marker into the default boot path, launch a native app, start hosted
  .NET, start Kestrel, or prove QEMU/cloud behavior.
