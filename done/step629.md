# Step 629 - Manifest Listen Port Shared Decimal Parser

## Summary

Migrated the kernel-facing native manifest listen-port parser from its local
ASCII digit accumulation loop to the shared `OS.Std.Text.AsciiDecimalParser`.
The manifest layer now keeps only listen-port policy: parsed decimal values
must be nonzero and fit in `ushort`.

## Why

Issue #3 added the shared no-runtime ASCII decimal parser so OS layers do not
grow duplicate decimal parsing helpers. Issue #5's manifest listen endpoint was
still carrying an ad hoc decimal loop. This slice moves that path onto the
shared helper while preserving the kernel-facing status-coded
`InvalidListenEndpoint` result for malformed, zero, and out-of-range ports.

## Files changed

- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `done/step629.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- The exact targeted host-test build passed with 0 warnings and 0 errors.
- Full host-test output was saved to `artifacts\logs\host-tests-step629.log`
  and ended with `TEST_PASS host.tests`.
- Relevant manifest markers from that log:
  - `TEST_PASS manifest kernel parses listen endpoint`
  - `TEST_PASS manifest kernel rejects invalid listen endpoint`
- Relevant shared parser markers from that log:
  - `TEST_PASS std.ascii.decimal parses uint32 values`
  - `TEST_PASS std.ascii.decimal parses uint64 values`
  - `TEST_PASS std.ascii.decimal rejects invalid input`
  - `TEST_PASS std.ascii.decimal default result is not success`
- `git diff --check` exited 0 with Git LF-to-CRLF normalization warnings only.

## Failures

- The first exact build attempt failed because another recent
  `OS.HostTests.exe` process held
  `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll` open. The process
  exited without intervention, and the exact build then passed.
- An isolated `--artifacts-path` host-test run was discarded because existing
  image-builder tests locate tools relative to the normal `bin` layout and
  failed before reaching useful full-harness evidence. The normal exact
  `dotnet run --project ... --no-build` path passed afterward.

## Deferred work

- Continue migrating future manifest/storage/network decimal parsing paths onto
  `OS.Std.Text.AsciiDecimalParser` when duplicate local loops appear.
- Keep live storage, app launch, hosted .NET 10, Minimal API, and Kestrel
  claims deferred until the matching completion-gate evidence exists.

## Evidence boundary

This is host-tested parser refactoring only. It does not change the manifest
schema, load manifests from live storage, bind sockets, create TCP listeners,
execute native apps, start hosted .NET 10, or prove Kestrel/cloud readiness.
