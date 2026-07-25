# Step 589 - PAL socket create default result guard

## Summary

Tightened `OSHostSocketCreateResult.Succeeded` so a default/zero socket-create
result no longer reports success. The guard preserves the existing POD layout
by requiring a valid mock socket handle in addition to `OSHostStatus.Ok`.

## Why

PAL socket creation is a hosted-runtime and Kestrel prerequisite. A zeroed
socket-create result has `OSHostStatus.Ok` by default, so success must also
require concrete socket-handle evidence before future socket, listener, or
hosted runtime code trusts this mock boundary.

## Files changed

- `src/OS.Kernel/Pal/OSHostSocketTable.cs`
- `tests/OS.HostTests/PalSocketTableTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `work/PAL/pal-design.md`
- `done/step589.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.socket table|pal\.socket accept|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host build succeeded with 0 warnings and 0 errors.
- Filtered host run ended with `TEST_PASS host.tests`.
- Socket markers included:
  - `TEST_PASS pal.socket table creates binds and listens`
  - `TEST_PASS pal.socket table rejects invalid lifecycle`
  - `TEST_PASS pal.socket table reports deterministic exhaustion`
  - `TEST_PASS pal.socket table create default result is not success`
  - `TEST_PASS pal.socket accept queues connection`
  - `TEST_PASS pal.socket accept rejects non-listener`
  - `TEST_PASS pal.socket accept reports empty queue`
- Full solution build succeeded with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed existing LF-to-CRLF working-copy
  warnings only.

## Failures

- No build, host-test, naming, source-firewall, or whitespace failures were
  observed for this slice.

## Deferred work

- This is host-tested in-process mock socket-table evidence only.
- `OSHostSocketAcceptResult`, `OSHostSocketOperationResult`, and
  `OSHostSocketPollResult` still need separate default-result guards.
- No exported PAL socket functions, live TCP, scheduler-backed socket waits,
  hosted .NET sockets, Kestrel, cloud readiness, or production readiness is
  proven by this step.

