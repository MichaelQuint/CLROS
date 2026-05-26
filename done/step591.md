# Step 591 - PAL socket operation default result guard and progress refresh

## Summary

Tightened `OSHostSocketOperationResult.Succeeded` so a default/zero socket
operation result no longer reports success. The guard preserves zero-byte
successful operations when they carry a valid socket handle, while rejecting the
all-zero default result as evidence.

Updated the README progress section to keep the project at the conservative
17% estimate and explain that the recent PAL result-guard hardening improves
hosted-runtime prerequisite quality without proving exported PAL, hosted .NET,
Kestrel, cloud-provider, or production-readiness gates.

## Why

PAL socket send/receive operation results are hosted-runtime and Kestrel
prerequisites. A zeroed operation result has `OSHostStatus.Ok` by default, so
success must also require concrete socket-handle evidence before future socket
or hosted runtime code trusts this ABI-shaped boundary.

The README needs to reflect current progress honestly: this work improves
contract hygiene but does not move the project beyond host-tested prerequisite
evidence.

## Files changed

- `README.md`
- `src/OS.Kernel/Pal/OSHostSocketAbi.cs`
- `tests/OS.HostTests/PalSocketAbiTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `work/PAL/pal-design.md`
- `done/step591.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.socket abi|pal\.socket send receive|pal\.socket partial|pal\.socket peer|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host build succeeded with 0 warnings and 0 errors.
- Filtered host run ended with `TEST_PASS host.tests`.
- Socket ABI and operation markers included:
  - `TEST_PASS pal.socket abi exposes endpoint contracts`
  - `TEST_PASS pal.socket abi rejects invalid endpoint values`
  - `TEST_PASS pal.socket abi exposes deterministic status codes`
  - `TEST_PASS pal.socket abi default operation result is not success`
  - `TEST_PASS pal.socket send receive transfers bytes`
  - `TEST_PASS pal.socket partial receive preserves unread bytes`
  - `TEST_PASS pal.socket partial send reports backpressure`
  - `TEST_PASS pal.socket peer close returns eof after draining`
  - `TEST_PASS pal.socket peer reset reports connection reset`
- Full solution build succeeded with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed existing LF-to-CRLF working-copy
  warnings only.

## Failures

- No build, host-test, naming, source-firewall, or whitespace failures were
  observed for this slice.

## Deferred work

- This is host-tested in-process socket ABI evidence only.
- `OSHostSocketPollResult` still needs a separate default-result guard.
- No exported PAL socket send/receive, live TCP, scheduler-backed socket waits,
  hosted .NET sockets, Kestrel, cloud readiness, or production readiness is
  proven by this step.
