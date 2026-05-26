# Step 704 - AsciiTokenSplitter Trim And Remove-Empty Options

## Summary
Extended `OS.Std.Text.AsciiTokenSplitter` with status-coded options for
ASCII whitespace trimming and empty-token removal while preserving the existing
default split behavior.

## Why
Manifest, app argument, and protocol parsing paths need shared no-runtime
token handling before kernel layers grow more ad hoc whitespace and empty-field
logic. This keeps the behavior allocation-free, caller-owned, and documented as
an OS-specific ASCII helper rather than a `System.String.Split` clone.

## Files changed
- `std/no-runtime/shared/Text/AsciiTokenSplitter.cs`
- `tests/OS.HostTests/StdAsciiTokenSplitterTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step704.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "std.ascii tokens|host.tests|TEST_FAIL"`

## Evidence
- Host-test build passed with 0 warnings and 0 errors.
- Focused host-test output emitted:
  - `TEST_PASS std.ascii tokens splits fields`
  - `TEST_PASS std.ascii tokens preserves empty fields`
  - `TEST_PASS std.ascii tokens trims fields`
  - `TEST_PASS std.ascii tokens removes empty fields`
  - `TEST_PASS std.ascii tokens trims then removes empty fields`
  - `TEST_PASS std.ascii tokens removes all empty fields`
  - `TEST_PASS std.ascii tokens reports destination capacity`
  - `TEST_PASS std.ascii tokens rejects invalid ranges`
  - `TEST_PASS std.ascii tokens rejects non ascii input`
  - `TEST_PASS std.ascii tokens compatibility boundary`
  - `TEST_PASS host.tests`

## Failures
- Initial build failed because a stale `requiredTokenCount++` reference remained
  after moving option-aware token counting into a helper. The reference was
  removed and the host-test project build then passed.

## Deferred work
- This is a partial `OS.Std.Text` helper only. It does not allocate strings,
  decode UTF-8, parse quoting/escaping, normalize paths, or implement
  `System.String.Split` compatibility.

