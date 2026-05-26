# Step 711 - Shared ASCII Key/Value Splitter

## Summary
Added a no-runtime-friendly `OS.Std.Text.AsciiKeyValueSplitter` helper and host
tests for issue #633. The splitter returns source-backed `AsciiTokenRange`
metadata for key/value pairs without allocation and preserves additional
delimiter bytes inside the value by splitting only on the first delimiter.

## Why
Manifest, protocol, and future package/config parsing paths need a shared
allocation-free key/value primitive before kernel layers migrate off local
one-off parsing. This keeps the behavior in `std/no-runtime/shared` first and
documents the compatibility boundary before any `NativeManifestParser`
migration.

## Files changed
- `std/no-runtime/shared/Text/AsciiKeyValueSplitter.cs`
- `tests/OS.HostTests/StdAsciiKeyValueSplitterTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step711.md`

## Tests run
- `dotnet build .\std\no-runtime\shared\OS.Std.NoRuntime.Shared.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- "std.ascii keyvalue"`
- `git diff --check`

## Evidence
- Shared std build passed with 0 warnings and 0 errors.
- Host-test build passed with 0 warnings and 0 errors.
- Focused host-test output emitted:
  - `TEST_PASS std.ascii keyvalue splits first delimiter`
  - `TEST_PASS std.ascii keyvalue preserves delimiter in value`
  - `TEST_PASS std.ascii keyvalue trims key and value`
  - `TEST_PASS std.ascii keyvalue allows empty value`
  - `TEST_PASS std.ascii keyvalue rejects empty key`
  - `TEST_PASS std.ascii keyvalue rejects missing delimiter`
  - `TEST_PASS std.ascii keyvalue rejects invalid ranges`
  - `TEST_PASS std.ascii keyvalue rejects invalid delimiter and options`
  - `TEST_PASS std.ascii keyvalue rejects non ascii input`
  - `TEST_PASS std.ascii keyvalue compatibility boundary`
  - `TEST_PASS host.tests`
- Evidence logs:
  - `artifacts/logs/issue633-std-build-20260525.log`
  - `artifacts/logs/issue633-host-build-20260525.log`
  - `artifacts/logs/issue633-host-keyvalue-20260525.log`
  - `artifacts/logs/issue633-git-diff-check-20260525.log`
- `git diff --check` exited 0. It printed existing CRLF normalization
  warnings for other dirty files in the worktree.

## Failures
- The first host-build retry was blocked by a concurrent `OS.HostTests.exe`
  process holding copied dependency DLLs. The process exited without
  intervention, and the retry passed.

## Deferred work
- `NativeManifestParser` was intentionally not migrated in this issue.
- Migrate `NativeManifestParser` key/value parsing to this shared helper in a
  later issue after reviewing compatibility and marker impact.

