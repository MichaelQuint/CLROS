# Step 820 - TCP Connection Table

## Summary

Added the host-tested caller-owned TCP connection table for established stream
metadata. The table stores value-only local/remote IPv4 endpoint keys, local and
remote sequence metadata, lifecycle state, and exposes add, lookup, update,
mark-closing, remove, inspect, and closed-slot reuse behavior over caller-owned
entry spans.

## Why

Gitea #777 required the next small TCP control-block metadata slice before
future socket delivery, retransmission, listener integration, or HTTP server
loops can rely on retained per-connection state. This keeps the contract
explicitly below timers, sockets, NIC I/O, and hosted workload behavior.

## Files changed

- `src/OS.Kernel/Net/TcpConnectionTable.cs`
- `tests/OS.HostTests/TcpConnectionTableTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step820.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
- Scoped active-issue `git diff --check`.
- Scoped active-issue trailing-whitespace scan.

## Evidence

- `artifacts/logs/issue777-repro-build.log` ended with `Build succeeded.`,
  `0 Warning(s)`, `0 Error(s)`, and `issue777-repro-build-exit=0`.
- `artifacts/logs/issue777-repro-host-tests.log` ended with
  `TEST_PASS host.tests` and `issue777-repro-host-tests-exit=0`.
- TCP connection table markers in `issue777-repro-host-tests.log`:
  - `TEST_PASS net.tcp.connection table adds established connection`
  - `TEST_PASS net.tcp.connection table rejects duplicate and capacity`
  - `TEST_PASS net.tcp.connection table looks up endpoint tuple`
  - `TEST_PASS net.tcp.connection table updates sequence metadata`
  - `TEST_PASS net.tcp.connection table closes removes and reuses slot`
  - `TEST_PASS net.tcp.connection table default result is not success`
  - `TEST_PASS net.tcp.connection table layouts contain no managed references`
- `artifacts/logs/active-issues-diff-check.log` ended with
  `active-issues-diff-check-exit=0`.
- `artifacts/logs/active-issues-trailing-whitespace.log` emitted
  `TEST_PASS active_issues.trailing_whitespace`.

## Failures

- Earlier host runs failed the close/remove/reuse marker while #777 was still
  in progress. The final run in `issue777-repro-host-tests.log` passed the
  marker and the full host executable.

## Deferred work

- This is host-tested TCP connection metadata only. It does not add timers,
  retransmission, socket delivery, HTTP server loops, live virtio-net packet
  I/O, hosted .NET, ASP.NET Minimal API, Kestrel, cloud readiness, or
  production readiness.

