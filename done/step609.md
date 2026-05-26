# Step 609 - ASCII String Span Helpers

## Summary

Added a compact host-tested `OS.Std.Text.AsciiString` helper slice for
ASCII ordinal-ignore-case prefix, suffix, and search operations over
`ReadOnlySpan<byte>`.

## Why

Issue #3 tracks shared no-runtime string and BCL-like helper gaps. Protocol,
manifest, and HTTP-facing code often needs byte-span ASCII comparisons without
allocating strings or relying on culture/Unicode behavior. This slice keeps that
behavior in `std/no-runtime/shared` before further OS-layer use.

## Files changed

- `std/no-runtime/shared/Text/AsciiString.cs`
  - Added `StartsWithIgnoreCase`, `EndsWithIgnoreCase`, and
    `IndexOfIgnoreCase` over byte spans.
- `tests/OS.HostTests/StdAsciiStringTests.cs`
  - Added host tests with stable `std.ascii.string` markers.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the helper subset, tests, compatibility boundary, and limits.
- `docs/completion-gates.md`
  - Recorded the host-tested Gate 4 evidence without changing gate status.
- `done/step609.md`
  - Recorded this step.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi
.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|std\.ascii\.string|host\.tests'
dotnet run --project .\artifacts\verify\step609\focused\focused.csproj
dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi
git diff --check
.\scripts\check-naming.ps1
```

## Evidence

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
  /p:OSWorkloadProfile=SingleMinimalApi` completed with 0 warnings and
  0 errors.
- Focused verifier compiled and ran the actual
  `tests/OS.HostTests/StdAsciiStringTests.cs` file through a temporary ignored
  project under `artifacts/verify/step609/focused` and emitted:
  - `TEST_PASS std.ascii.string startswith endswith ignorecase`
  - `TEST_PASS std.ascii.string indexof ignorecase`
  - `TEST_PASS std.ascii.string compatibility boundary`
  - `TEST_PASS std.ascii.string focused`
- `dotnet build .\OS.slnx --nologo -m:1
  /p:OSWorkloadProfile=SingleMinimalApi` completed with 0 warnings and
  0 errors.
- `git diff --check` completed with no whitespace errors; it printed existing
  LF-to-CRLF working-copy warnings for dirty files.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.

## Failures

- The first host-test build without an explicit workload profile failed in
  existing `Directory.Build.targets` validation for `OSWorkloadProfile=''` on
  `tools/OS.QemuRunner`. The build passed after rerunning with
  `/p:OSWorkloadProfile=SingleMinimalApi`.
- The full host-test executable run did not reach the new std markers. It
  failed first in existing `CiSmokeScriptTests` with
  `System.InvalidOperationException: CI smoke script tests failed: 1` after
  emitting unrelated `ci.smoke` and `libvirt.smoke.generate_xml` failure
  markers.
- The first focused verifier scaffold attempt used `dotnet new console
  --nologo`, which is not a valid `dotnet new` option. The scaffold was rerun
  without that option.

## Deferred work

- This is not a `System.String` implementation and does not add substring,
  split, join, concat, collation, Unicode normalization, string allocation, or
  `StringBuilder`.
- No kernel integration, runtime helper, GC, exception, interface dispatch,
  hosted .NET, ASP.NET Minimal API, or Kestrel behavior is implemented.
