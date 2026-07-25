# Step 626 - std ASCII Decimal Parser

## Summary
Added a no-runtime-compatible ASCII decimal parser under `OS.Std.Text`.
The parser handles unsigned `uint` and `ulong` decimal byte spans, returns
status-coded value-type results, detects overflow before mutating the parsed
value, and keeps default/zero results from counting as success evidence.

## Why
Protocol, manifest, and diagnostic paths need small shared numeric parsing
helpers before ad hoc OS-layer copies appear. This slice keeps parsing
ASCII-only, allocation-free, and explicit about unsupported signs, whitespace,
Unicode digits, culture, and partial-token behavior.

## Files changed
- `std/no-runtime/shared/Text/AsciiDecimalParser.cs`
- `tests/OS.HostTests/StdAsciiDecimalParserTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step626.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\artifacts\codex-std-decimal-runner\StdDecimalParserRunner.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi`
- `dotnet run --project .\artifacts\codex-std-decimal-runner\StdDecimalParserRunner.csproj --no-build /p:OSWorkloadProfile=SingleMinimalApi`
- `dotnet run --project .\artifacts\codex-std-decimal-runner\StdDecimalParserRunner.csproj --no-build /p:OSWorkloadProfile=SingleMinimalApi | Tee-Object -FilePath .\artifacts\logs\std-ascii-decimal-runner.log`
- `git diff --check`

## Evidence
- The profiled host-test build completed with 0 errors. It emitted copy-retry
  warnings because an existing `OS.HostTests` process with PID `11840` held
  normal output DLLs open, but MSBuild eventually produced
  `tests/OS.HostTests/bin/Debug/net10.0/OS.HostTests.dll`.
- The isolated decimal-parser runner build completed with 0 warnings and
  0 errors.
- `artifacts/logs/std-ascii-decimal-runner.log` contains:
  - `TEST_PASS std.ascii.decimal parses uint32 values`
  - `TEST_PASS std.ascii.decimal parses uint64 values`
  - `TEST_PASS std.ascii.decimal rejects invalid input`
  - `TEST_PASS std.ascii.decimal default result is not success`
  - `TEST_PASS std.ascii.decimal.runner`
- `git diff --check` exited 0 and reported only Git LF-to-CRLF conversion
  warnings for existing dirty files.

## Failures
- The first host-test build without an explicit workload profile failed in
  existing `Directory.Build.targets` validation:
  `Invalid OSWorkloadProfile ''. Valid values: SingleMinimalApi, MultiMinimalApi.`
- The full host-test run failed in an existing QEMU runner test before the std
  module initializer group completed:
  `TEST_FAIL qemu.smoke.preset acpi fails when MCFG marker is missing` and
  `TEST_FAIL qemu.runner.tests failures=1`.
- No Gitea issue comment was posted because no Gitea token or credentials were
  present in the task context or environment.

## Deferred work
- Integrate the parser into protocol and manifest readers where it removes
  local numeric parsing without widening this slice's write scope.
- Add signed or partial-token parsing only when a concrete no-runtime caller
  needs it and the supported subset can be documented and tested.

## Evidence boundary

This is host-tested shared std helper evidence only. It does not complete the
BCL subset, does not implement NativeAOT runtime helpers, GC, exceptions,
class constructors, thread statics, hosted .NET 10, Kestrel, live storage,
live networking, or cloud readiness.

