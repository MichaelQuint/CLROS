# Step 633 - std ASCII unsigned decimal formatter

## Summary

Added `OS.Std.Text.AsciiDecimalFormatter`, a focused allocation-free unsigned
ASCII decimal formatter for `uint` and `ulong` values into caller-owned
`Span<byte>` buffers.

## Why

The no-runtime text surface already had decimal parsing and decimal append
support inside `AsciiTextBuilder`, but no standalone formatter result contract
for callers that only need unsigned decimal bytes. This keeps the helper under
`OS.Std.Text` rather than claiming a `System.*` compatibility surface.

## Files changed

- `std/no-runtime/shared/Text/AsciiDecimalFormatter.cs`
- `tests/OS.HostTests/StdAsciiDecimalFormatterTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step633.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Reserved this evidence file with `.\scripts\new-done-step.ps1 -Reserve`:
  - `TEST_START done.step.allocate`
  - `NEXT_DONE_STEP done/step633.md`
  - `TEST_PASS done.step.allocate`
- Focused build completed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS std.ascii.decimal.formatter formats uint32 values`
  - `TEST_PASS std.ascii.decimal.formatter formats uint64 values`
  - `TEST_PASS std.ascii.decimal.formatter reports capacity`
  - `TEST_PASS std.ascii.decimal.formatter default result is not success`
  - `TEST_PASS host.tests`
- `git diff --check` passed.

## Failures

- None.
- The full host-test run exercised the done-step allocator tests and created
  additional reserved templates `done/step634.md` through `done/step638.md`.
  They were left untouched under the immutable done-file rule.

## Deferred work

- No `System.*`, `System.Number`, or `ISpanFormattable` compatibility surface
  was added.
- No signed integer, padding, format-string, hexadecimal, floating-point, or
  culture-specific formatter was added.
- `AsciiTextBuilder` was left intact; a future cleanup can choose whether to
  delegate its decimal append path to the standalone formatter after the
  parallel text-helper work settles.

## Evidence boundary

This is host-tested `std/no-runtime` helper evidence only. It does not
implement runtime allocation helpers, GC, exception handling, hosted .NET 10,
ASP.NET Minimal API, Kestrel, live firmware behavior, or cloud readiness.
