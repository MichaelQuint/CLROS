# Step 585 - PAL package-file default result guard

## Summary

Added initialized-state metadata to the PAL package-file table open, read, and
enumeration result structs so a default/zero result no longer reports success.
The package-file host tests now prove that default results are rejected while
preserving the existing package open/read/enumeration lifecycle behavior.

## Why

The future `OSHost_*` file ABI must not let a zeroed result value cross a
kernel/runtime boundary as successful evidence. Package file access is a
Kestrel-critical prerequisite for hosted app assemblies and configuration, so
its mock in-process model needs the same default-result safety pattern as the
other PAL readiness slices.

## Files changed

- `src/OS.Kernel/Pal/OSHostPackageFileTable.cs`
- `tests/OS.HostTests/PalPackageFileTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step585.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host build succeeded with 0 warnings and 0 errors.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full host run ended with `TEST_PASS host.tests`.
- Package-file markers included:
  - `TEST_PASS pal.package facade opens reads info closes`
  - `TEST_PASS pal.package facade reports missing and unsafe paths`
  - `TEST_PASS pal.package facade reports small read buffers`
  - `TEST_PASS pal.package facade rejects stale handles`
  - `TEST_PASS pal.package facade reports handle exhaustion`
  - `TEST_PASS pal.package facade enumerates package files`
  - `TEST_PASS pal.package facade rejects malformed request shapes`
  - `TEST_PASS pal.package facade result layouts contain no managed references`
  - `TEST_PASS pal.file opens package file`
  - `TEST_PASS pal.file reads offset ranges`
  - `TEST_PASS pal.file rejects unsafe paths`
  - `TEST_PASS pal.file rejects invalid handles`
  - `TEST_PASS pal.file reports deterministic exhaustion`
  - `TEST_PASS pal.file enumerates package files`
  - `TEST_PASS pal.file enumeration reports buffer too small`
  - `TEST_PASS pal.file default results are not success`
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed existing LF-to-CRLF working-copy
  warnings only.

## Failures

- Claude CLI delegation was retried and still failed authentication with
  `Not logged in - Please run /login`; no credentials were passed to Claude.
- No build, host-test, naming, source-firewall, or whitespace failures were
  observed for this slice.

## Deferred work

- This is host-tested in-process package-file table/facade evidence only.
- No exported PAL file functions, live guest storage reads, hosted .NET file
  access, in-guest Kestrel behavior, Azure/AWS cloud readiness, or production
  readiness is proven by this step.
