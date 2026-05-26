# Step 620 - Std ASCII Text Builder

## Summary

Added `OS.Std.Text.AsciiTextBuilder`, a caller-owned ASCII byte-buffer builder
for no-runtime-friendly diagnostics and protocol formatting. It appends ASCII
byte spans, single bytes, ASCII chars, unsigned/signed decimal integers, and
padded uppercase hex integers while reporting status-coded required-byte
metadata.

## Why

Several kernel paths still need bounded text formatting for panic, serial,
manifest, HTTP, and PAL diagnostics. This helper keeps that functionality in
`std/no-runtime/shared` first, avoids `System.Text.StringBuilder` allocation
semantics, rejects unsupported input explicitly, and reuses the shared
`BufferOperations.MemoryCopy` path for overlap-safe byte appends.

## Files changed

- `std/no-runtime/shared/Text/AsciiTextBuilder.cs`
- `tests/OS.HostTests/StdAsciiTextBuilderTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step620.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe`
- `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host-test build completed with 0 warnings and 0 errors.
- Full host-test log: `artifacts/logs/host-tests-step620.log`.
- Machine-readable markers:
  - `TEST_PASS std.ascii.builder appends bytes and chars`
  - `TEST_PASS std.ascii.builder appends decimal and hex`
  - `TEST_PASS std.ascii.builder rejects capacity and input`
  - `TEST_PASS std.ascii.builder default result is not success`
  - `TEST_PASS host.tests`
- `dotnet build .\OS.slnx ...` completed with 0 warnings and 0 errors.
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` reported only Git CRLF conversion warnings; no whitespace
  errors were reported.

## Failures

- No compile, host-test, naming, source-firewall, or whitespace failures were
  observed for this slice.

## Deferred work

- Migrate panic, serial, HTTP, manifest, and PAL diagnostic formatting call
  sites onto `AsciiTextBuilder` where it removes ad hoc formatting or
  allocation-sensitive string usage.
- Add UTF-8 or wider formatting helpers only after the supported contract and
  no-runtime behavior are documented.
- Keep canonical `System.Text.StringBuilder` unavailable until a compatible BCL
  contract is implemented and documented.

## Evidence boundary

This is host-tested std/no-runtime helper evidence only. It does not implement
the BCL `StringBuilder`, managed exception handling, runtime allocation helpers,
GC, class constructors, hosted .NET 10, Kestrel, or cloud readiness.
