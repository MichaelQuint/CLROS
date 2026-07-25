# Step 823 - Phase C Std ASCII Hex Parser

## Summary

Added `OS.Std.Text.AsciiHexParser`, an allocation-free ASCII hexadecimal parser
for unsigned `uint` and `ulong` values over `ReadOnlySpan<byte>`.

## Why

Gitea #781 needed a Phase C std/no-runtime parser slice modeled on the existing
ASCII decimal parser contracts. The new helper keeps parsing status-coded,
caller-owned, and value-type-only so future kernel/runtime paths can parse
diagnostic or protocol hexadecimal values without exceptions, allocation, or
managed references in result metadata.

## Files changed

- `std/no-runtime/shared/Text/AsciiHexParser.cs`
- `tests/OS.HostTests/StdAsciiHexParserTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step823.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `.\scripts\build-efi-shell.ps1`
- Scoped `git diff --check` for the issue write scope.
- Scoped trailing-whitespace scan for the issue write scope, including
  untracked new files.

## Evidence

- `artifacts/logs/issue781-build.log` contains `Build succeeded.`
- `artifacts/logs/issue781-hosttests-build.log` contains `Build succeeded.`
- `artifacts/logs/issue781-hosttests.log` contains `TEST_PASS host.tests`.
- `artifacts/logs/issue781-hosttests.log` contains:
  - `TEST_PASS std.ascii.hex parses uint32 values`
  - `TEST_PASS std.ascii.hex parses uint64 values`
  - `TEST_PASS std.ascii.hex accepts optional 0x prefix`
  - `TEST_PASS std.ascii.hex rejects invalid input`
  - `TEST_PASS std.ascii.hex rejects overflow`
  - `TEST_PASS std.ascii.hex default result is not success`
  - `TEST_PASS std.ascii.hex layouts contain no managed references`
- `artifacts/logs/issue781-check-naming.log` contains `TEST_PASS naming.guard`
  and `TEST_PASS source.firewall`.
- `artifacts/logs/issue781-qemu-dryrun.log` contains
  `TEST_PASS qemu.runner.dryrun`.
- `artifacts/logs/issue781-efi-shell.log` contains
  `TEST_PASS efi.shell.publish` and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- `artifacts/logs/issue781-diff-check-scoped.log` completed with exit code 0.
- `artifacts/logs/issue781-whitespace-scoped.log` contains
  `SCOPED_WHITESPACE_OK`.

## Failures

- None for this issue slice.

## Deferred work

- This is host-tested std/no-runtime helper evidence only. It does not change
  runtime helper completion, live boot behavior, storage, networking,
  platform, `BootSequence`, or completion-gate status.
