# Step 641 - Signed ASCII Decimal Formatter

## Summary

Extended `OS.Std.Text.AsciiDecimalFormatter` with allocation-free signed
decimal formatting for `int` and `long` into caller-owned ASCII byte spans.
The formatter handles `int.MinValue` and `long.MinValue` without overflow,
reports required byte counts, rejects undersized destinations before writing,
and preserves the existing default-result rejection contract.

## Why

Kernel, diagnostics, and app-model code need shared no-runtime formatting
helpers before ad hoc signed integer formatting spreads through OS layers.
This keeps signed decimal output in `std/no-runtime/shared` first, matching the
repo invariant for BCL-like/shared utilities.

## Files changed

- `std/no-runtime/shared/Text/AsciiDecimalFormatter.cs`
- `tests/OS.HostTests/StdAsciiDecimalFormatterTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step641.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- std/no-runtime/shared/Text/AsciiDecimalFormatter.cs tests/OS.HostTests/StdAsciiDecimalFormatterTests.cs docs/nativeaot-nostdlib-limits.md docs/completion-gates.md done/step641.md`

## Evidence

- `artifacts/logs/host-tests-step641-rerun.log` contains:
  - `TEST_PASS std.ascii.decimal.formatter formats uint32 values`
  - `TEST_PASS std.ascii.decimal.formatter formats uint64 values`
  - `TEST_PASS std.ascii.decimal.formatter formats int32 values`
  - `TEST_PASS std.ascii.decimal.formatter formats int64 values`
  - `TEST_PASS std.ascii.decimal.formatter reports capacity`
  - `TEST_PASS std.ascii.decimal.formatter reports signed capacity`
  - `TEST_PASS std.ascii.decimal.formatter default result is not success`
  - `TEST_PASS host.tests`
- The host-test build passed with zero warnings and zero errors.

## Failures

- A normal build initially failed with `MSB3027` because two other
  `OS.HostTests` processes from concurrent worker verification held the default
  output DLLs. After the worker processes exited, the same normal build passed.
- An attempted isolated output/intermediate build under per-project
  `artifacts/build/std-signed` directories failed because generated
  assembly-info files under a tool project directory were picked up by SDK
  source globs. Those temporary generated directories were removed.
- `git diff --check` reported only existing LF-to-CRLF warnings for touched
  markdown files.

## Deferred work

- This is a no-runtime shared formatter slice only. It does not implement
  `System.ISpanFormattable`, culture/format-string handling, padding,
  hexadecimal formatting, floating-point formatting, or a canonical
  `System.*` formatter contract.
- Existing callers were not migrated in this step.
