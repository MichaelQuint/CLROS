# Step 645 - std/no-runtime signed ASCII decimal parsing

## Summary

Added the smallest signed ASCII decimal parsing slice to
`OS.Std.Text.AsciiDecimalParser` for `int` and `long`, matching the existing
unsigned result style and preserving no-allocation/status-coded behavior.

## Why

Shared no-runtime parser code needs signed integer coverage before kernel,
manifest, protocol, or diagnostics code grows ad hoc decimal parsing. This
keeps the compatibility boundary explicit and host-tested while avoiding
exceptions, culture, allocation, and partial-token parsing.

## Files changed

- `std/no-runtime/shared/Text/AsciiDecimalParser.cs`
  - Added `AsciiInt32ParseResult`, `AsciiInt64ParseResult`, `ParseInt32`,
    `ParseInt64`, `TryParseInt32`, and `TryParseInt64`.
  - Added `SignOnly` status for rejected sign-only signed input.
  - Signed parsers accept a leading `-`, reject `+`, reject whitespace and
    non-digits, and detect positive/negative overflow including MinValue.
- `tests/OS.HostTests/StdAsciiDecimalParserTests.cs`
  - Added signed Int32/Int64 value, MinValue/MaxValue, invalid input,
    overflow, TryParse-clearing, no-managed-reference layout, and default
    result coverage.
  - Added explicit unsigned `-` rejection coverage alongside existing `+`
    rejection.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the signed parser subset and the no-leading-plus convention.
- `done/step645.md`
  - Records this evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- std/no-runtime/shared/Text/AsciiDecimalParser.cs tests/OS.HostTests/StdAsciiDecimalParserTests.cs docs/nativeaot-nostdlib-limits.md done/step645.md`

## Evidence

- Focused host-test project build passed with 0 warnings and 0 errors.
- Full host-test run emitted:
  - `TEST_PASS std.ascii.decimal parses uint32 values`
  - `TEST_PASS std.ascii.decimal parses uint64 values`
  - `TEST_PASS std.ascii.decimal parses int32 values`
  - `TEST_PASS std.ascii.decimal parses int64 values`
  - `TEST_PASS std.ascii.decimal rejects invalid input`
  - `TEST_PASS std.ascii.decimal rejects signed invalid input`
  - `TEST_PASS std.ascii.decimal default result is not success`
  - `TEST_PASS host.tests`

## Failures

No persistent failures in the final validation. One intermediate rerun of the
focused build failed with `MSB3027` because an `OS.HostTests` process was still
holding copied output DLLs; the lock cleared and the same build command passed
on rerun.

## Deferred work

This is host-tested `std/no-runtime` utility evidence only. It does not add a
canonical `System.Number` or `System.Int32/Int64` parser, does not add
whitespace/culture/thousands/hex/floating-point/partial-token parsing, does not
migrate kernel callers, and does not prove runtime helper, hosted .NET,
Kestrel, storage, networking, cloud, or production readiness.
