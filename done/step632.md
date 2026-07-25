# Step 632 - PAL config/environment table capacity contract

## Summary

Added a fixed-capacity creation boundary for the host-only PAL config/environment
table. `OSHostConfigEnvironmentTable.Create` now returns a status-coded build
result, accepts bounded caller-provided key/value entries, and rejects config or
environment entry counts above the table capacity with `OSHostStatus.OutOfMemory`.

## Why

The future `OSHost_*` config/environment ABI must stay deterministic and
caller-owned. Modeling capacity at the host-tested table boundary keeps the
planning contract explicit before exported PAL functions or hosted .NET
configuration exist.

## Files changed

- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
- `tests/OS.HostTests/PalConfigEnvironmentFacadeTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `done/step632.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Reserved this evidence file with `.\scripts\new-done-step.ps1 -Reserve`:
  - `TEST_START done.step.allocate`
  - `NEXT_DONE_STEP done/step632.md`
  - `TEST_PASS done.step.allocate`
- Build retry completed with 0 warnings and 0 errors.
- Host tests completed with:
  - `TEST_PASS pal.config facade returns config and env values`
  - `TEST_PASS pal.config table builds from native manifest`
  - `TEST_PASS pal.config table reports deterministic capacity`
  - `TEST_PASS pal.config table preserves host-only boundary`
  - `TEST_PASS host.tests`
- `git diff --check` passed.

## Failures

- The first build attempt failed because another host-test process held output
  DLLs under `tests/OS.HostTests/bin/Debug/net10.0`. No external process was
  killed; the same build command was retried after the lock cleared and passed.

## Deferred work

- No unmanaged `OSHost_GetConfigValue`, `OSHost_GetEnvironmentValue`, or
  enumeration exports were added.
- No guest PAL, hosted CoreCLR/.NET configuration provider, ASP.NET
  configuration bridge, guest storage-backed config, or live Kestrel behavior
  exists in this slice.
- The table remains host-tested in-process planning/contract evidence only.

## Evidence boundary

This is host-tested PAL contract/planning evidence only. It does not prove
hosted CoreCLR, hosted .NET 10, ASP.NET Minimal API, Kestrel, live PAL exports,
guest storage, or production/cloud readiness.

