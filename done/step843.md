# Step 843 - M00-019 Bounded Serial Evidence

## Summary

Hardened serial marker parsing and QEMU process integration across arbitrary
chunks, firmware noise, failure terminals, timeouts, and cleanup races.

## Why

Marker proof must fail closed rather than accept substrings, stale success,
truncated logs, conflicting terminals, `TEST_FAIL`, or child-process failure.

## Files changed

- `tools/OS.QemuRunner/SerialMarkerStateMachine.cs`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/SerialMarkerStateMachineTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`

## Tests run

```powershell
dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The parser is bounded to 4 KiB lines and 16 MiB total input, requires exactly
one success terminal, and distinguishes structural, timeout, EOF, missing,
forbidden, duplicate, and conflict failures. Integration tests cover additive
custom markers, late terminals, `TEST_FAIL`, nonzero exit before kill, hard
timeout precedence, prelaunch validation, resolved tool/firmware aliases,
unchanged aliased bytes, and bounded process cleanup.

## Failures

Independent review found and drove fixes for custom-marker false passes,
ignored nonzero exits, post-launch option validation, soft deadline ordering,
unbounded output/final reads, artifact aliasing, intent-versus-issued-kill
races, discovered-path aliases, and the missing default `TEST_FAIL` exclusion.

## Deferred work

CR-only line sequences remain intentionally unsupported; LF and CRLF are the
documented contract.

## Evidence boundary

Host tests prove runner/parser behavior. Step 846 binds the real guest serial
bytes to the current EFI/image evidence.
