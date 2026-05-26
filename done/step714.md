# Step 714 - Native Manifest Shared Key/Value Splitter Migration

## Summary
Migrated the kernel-facing native manifest parser's line-level `key=value`
split to the shared `OS.Std.Text.AsciiKeyValueSplitter` helper. The parser now
uses the same first-delimiter, source-backed key/value primitive that future
manifest, config, PAL environment, and protocol parsers can share.

## Why
The project invariant requires shared string/text utilities to live in
`std/no-runtime/shared` before kernel layers grow local ad hoc behavior. The
manifest parser already depended on first-`=` splitting that preserves
additional delimiter bytes inside values, so moving that behavior onto the
shared helper reduces duplication without changing the manifest format.

## Files changed
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `docs/manifest.md`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step714.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- "manifest kernel"`

## Evidence
- Focused host-test log: `artifacts\logs\hosttests-issue635-20260525.log`
- Build passed with 0 warnings and 0 errors.
- Focused host-test command exited 0 and emitted:
  - `TEST_PASS manifest kernel preserves token splitter semantics`
  - `TEST_PASS manifest kernel preserves equals inside values`
  - `TEST_PASS manifest kernel uses shared key value splitter`
  - `TEST_PASS host.tests`

## Failures
- None.

## Deferred work
- Other local key/value parsing call sites, including future PAL config and
  protocol header paths, still need to be audited and migrated where the shared
  splitter contract fits.

