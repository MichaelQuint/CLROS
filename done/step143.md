# Step 143 - TCP Close/Reset Transition Primitive

## What Changed

- Added `OS.Net.TcpConnectionClose` as a deterministic established-connection close/reset transition planner.
- Added status-coded close states and transition results for:
  - active close: `ESTABLISHED -> FIN_WAIT_1 -> FIN_WAIT_2 -> TIME_WAIT`;
  - passive close: `ESTABLISHED -> CLOSE_WAIT -> LAST_ACK -> CLOSED`;
  - inbound reset: non-terminal close states to `RESET`;
  - invalid transition rejection without unexpected state advancement.
- Added host-test source with the required issue #161 markers:
  - `TEST_PASS net.tcp close active fin ack`
  - `TEST_PASS net.tcp close passive fin ack`
  - `TEST_PASS net.tcp close handles reset`
  - `TEST_PASS net.tcp close rejects invalid transition`
- Updated networking and completion-gate docs with the host-test-only boundary.

## Why It Changed

Gitea issue #161 needed the next TCP state-machine slice after established payload handling. This adds deterministic FIN/ACK and RST planning for already-established connections without adding timers, retransmission, congestion control, sockets, virtio-net I/O, TLS, hosted .NET, or Kestrel behavior.

## Files Changed

- `src/OS.Kernel/Net/TcpConnectionClose.cs`
- `tests/OS.HostTests/TcpConnectionCloseTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step143.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\artifacts\focused\Issue161TcpClose\Issue161TcpClose.csproj --nologo
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- The focused temporary harness linked only `tests/OS.HostTests/TcpConnectionCloseTests.cs` so broader host-test module initializers did not run.
- The focused harness emitted:

```text
TEST_PASS net.tcp close active fin ack
TEST_PASS net.tcp close passive fin ack
TEST_PASS net.tcp close handles reset
TEST_PASS net.tcp close rejects invalid transition
```

## Failures

- No focused build or #161 focused test failures were observed.
- Full host-test execution was intentionally not run because issue #161 was limited to focused verification.

## Deferred Work

- No retransmission, timers, congestion control, receive-window negotiation, simultaneous close, `TIME_WAIT` cleanup, TCP control-block table, socket/PAL export, live virtio-net I/O, TLS, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.
- Issue #161 was not closed.

## Evidence Boundary

This step is managed build-verified and focused-host-test-verified TCP close/reset planning only. It proves the new kernel-facing source and focused test source compile and that the four required #161 markers pass when only the #161 test file is linked into a temporary harness. It does not prove full host-test execution, live packet I/O, a full TCP state machine, sockets, FetchApp, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
