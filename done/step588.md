# Step 588 - PAL wait event default result guard

## Summary

Tightened `OSHostEventCreateResult.Succeeded` so a default/zero event-create
result no longer reports success. The guard preserves the existing POD layout
by requiring a valid opaque wait handle in addition to `OSHostStatus.Ok`.

## Why

PAL wait/event behavior is a hosted-runtime and Kestrel prerequisite. A zeroed
event-create result has `OSHostStatus.Ok` by default, so success must also
require concrete wait-handle evidence before future thread/sync code trusts the
mock boundary.

## Files changed

- `src/OS.Kernel/Pal/OSHostWaitTable.cs`
- `tests/OS.HostTests/PalWaitTableTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `work/PAL/pal-design.md`
- `done/step588.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.wait|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host build succeeded with 0 warnings and 0 errors.
- Filtered host run ended with `TEST_PASS host.tests`.
- Wait markers included:
  - `TEST_PASS pal.wait facade creates manual and auto events`
  - `TEST_PASS pal.wait facade set reset close translate statuses`
  - `TEST_PASS pal.wait facade wait one reports signal and timeout`
  - `TEST_PASS pal.wait facade wait many reports first signaled`
  - `TEST_PASS pal.wait facade translates invalid and stale handles`
  - `TEST_PASS pal.wait facade rejects unsupported wait all and timeout`
  - `TEST_PASS pal.wait facade rejects null table`
  - `TEST_PASS pal.wait facade reports exhaustion`
  - `TEST_PASS pal.wait facade layouts are stable`
  - `TEST_PASS pal.wait manual reset event stays signaled`
  - `TEST_PASS pal.wait auto reset consumes signal`
  - `TEST_PASS pal.wait many reports first signaled`
  - `TEST_PASS pal.wait reset and close reject invalid`
  - `TEST_PASS pal.wait invalid options are rejected`
  - `TEST_PASS pal.wait default results are not success`
- Full solution build succeeded with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed existing LF-to-CRLF working-copy
  warnings only.

## Failures

- No build, host-test, naming, source-firewall, or whitespace failures were
  observed for this slice.
- Claude Code CLI is still unavailable from this shell for subagent work:
  `Not logged in - Please run /login`.

## Deferred work

- This is host-tested in-process mock wait/event-table evidence only.
- No exported PAL wait/event functions, blocking waits, scheduler park/wake
  integration, timer-backed blocking waits, hosted .NET synchronization,
  Kestrel, cloud readiness, or production readiness is proven by this step.

