# Step 584 - PAL config/environment default-result guards

## Summary

`OSHostConfigQueryResult` and `OSHostEnvironmentEnumerationResult` now carry
initialized-state metadata and require it for `Succeeded`. Default/zero PAL
config query and environment enumeration results can no longer be interpreted
as successful evidence.

## Why

The config/environment table projects manifest metadata into host-tested
queries for future hosted runtime configuration. Its query/enumeration result
structs had success-shaped default status values. This step keeps the model
host-only while making default results explicit non-success values.

## Files changed

- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step584.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.config|pal\.env|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build succeeded with `0 Warning(s)` and `0 Error(s)`.
- Filtered host-test markers:
  - `TEST_PASS pal.config returns manifest config value`
  - `TEST_PASS pal.config reports buffer too small`
  - `TEST_PASS pal.env returns manifest environment value`
  - `TEST_PASS pal.env enumerates environment entries`
  - `TEST_PASS pal.env rejects invalid queries`
  - `TEST_PASS pal.config table builds from native manifest`
  - `TEST_PASS pal.config table exposes manifest listen endpoint`
  - `TEST_PASS pal.config table exposes listen host and port`
  - `TEST_PASS pal.config table default results are not success`
  - `TEST_PASS pal.config facade returns config and env values`
  - `TEST_PASS host.tests`
- Full solution build succeeded with `0 Warning(s)` and `0 Error(s)`.
- Naming/source firewall emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited `0`; output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- None.

## Deferred work

- Config/environment coverage remains host-tested in-process table/facade
  evidence only. It does not export PAL config/environment functions, feed
  hosted runtime configuration, feed ASP.NET configuration, start Kestrel,
  prove cloud readiness, or prove production readiness.
