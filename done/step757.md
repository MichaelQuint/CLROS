# Step 757 - PAL Config Listen-Port ASCII Builder Migration

## Summary

Migrated `OSHostConfigEnvironmentTable.PortBytes(ushort)` from a PAL-local
reversed decimal loop to the shared no-runtime ASCII text helpers.

## Why

Listen-port config values are part of the PAL config/environment table
boundary. Numeric byte formatting for that boundary should use the shared
`OS.Std.Text` helpers instead of subsystem-local decimal loops so future
no-runtime formatting behavior stays centralized and source-guarded.

## Files changed

- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `done/step757.md`
- `artifacts/logs/issue715-host-build-20260525.log`
- `artifacts/logs/issue715-hosttests-20260525.log`
- `artifacts/logs/issue715-git-diff-check-20260525.log`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue715-pal-port-format\bin\Debug\net10.0`
- `dotnet .\artifacts\issue715-pal-port-format\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- Build completed with `Build succeeded.`, 0 warnings, and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- PAL config markers observed:
  - `TEST_PASS pal.config table exposes manifest listen endpoint`
  - `TEST_PASS pal.config table exposes listen host and port`
  - `TEST_PASS pal.config facade returns manifest listen endpoint`
  - `TEST_PASS pal.config table port formatter uses shared ascii text builder`
- The new source guard verifies
  `AsciiDecimalFormatter.CountUInt64Digits(port)` and
  `AsciiTextBuilder.AppendUInt64Decimal(bytes, 0, port)` are present, and that
  the old `Span<byte> reversed`, `value % 10`, `value /= 10`, and reverse-copy
  pattern are absent from `OSHostConfigEnvironmentTable.cs`.
- `git diff --check` returned exit code 0 with only existing line-ending
  warnings in the captured log.

## Failures

- No build or host-test failures.
- The first PowerShell `Tee-Object` invocation of `git diff --check` surfaced
  Git line-ending warnings as a native command error record. The command was
  rerun with cmd redirection; Git returned exit code 0 and the requested log was
  preserved at `artifacts/logs/issue715-git-diff-check-20260525.log`.

## Deferred work

- This is host-tested config/environment table evidence only. It does not
  export PAL symbols, materialize hosted .NET environment variables, start
  Kestrel, open sockets, read guest storage, or prove any live guest behavior.

