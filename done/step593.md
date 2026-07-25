# Step 593 - PAL socket poll default-result guard

## Summary

`OSHostSocketPollResult` now carries explicit initialized-state evidence so a
default/zero result does not count as successful poll readiness. Valid
table-produced zero-event polls still succeed.

## Why

`Status == OSHostStatus.Ok` alone made `default(OSHostSocketPollResult)` look
successful because `OSHostStatus.Ok` is zero. That weakened the PAL/Kestrel mock
evidence boundary by allowing uninitialized poll results to be mistaken for
real readiness.

## Files changed

- `src/OS.Kernel/Pal/OSHostSocketTable.cs`
- `tests/OS.HostTests/PalSocketTableTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `work/PAL/pal-design.md`
- `done/step593.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.socket poll|pal\.socket table|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host-test project build passed with 0 warnings and 0 errors.
- Focused host-test output included:
  - `TEST_PASS pal.socket poll reports close and reset readiness`
  - `TEST_PASS pal.socket poll reports readiness`
  - `TEST_PASS pal.socket poll default result is not success`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- Naming/source firewall emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` reported no whitespace errors. It did report existing
  LF-to-CRLF working-copy normalization warnings for the touched files.

## Failures

- No test failures in the commands above.
- No QEMU or exported PAL validation was run because this step is host-tested
  in-process mock socket evidence only.

## Deferred work

- No `OSHost_SocketPoll` unmanaged export was added.
- No live socket behavior, scheduler wait, TCP packet path, hosted .NET runtime,
  Minimal API, or Kestrel behavior was implemented or claimed.
- Future exported PAL work still needs caller-owned poll entry mutation,
  blocking/wakeup semantics, and integration with the scheduler and live
  network stack.

