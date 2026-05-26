# Step 619 - TCP Receive Dispatcher Host Slice

## Summary

Added `TcpReceiveDispatcher` as a host-tested receive-side TCP routing boundary
over caller-owned virtio-net receive buffers and already-parsed
`NetworkReceiveDispatchResult` metadata.

The dispatcher validates IPv4/TCP checksums before mutation, routes established
payload segments into `TcpEstablishedStream`, routes passive FIN/ACK and RST
segments into `TcpConnectionClose`, and writes a passive-close ACK segment into
caller-owned storage with a computed TCP checksum.

## Why

Issue #6 needs networking/Kestrel prerequisites that can progress without live
virtio-net. This slice narrows the gap between packet parsing and future socket
or TCP-control-block integration while keeping the current evidence boundary
honest: host-tested only, caller-owned buffers only, no socket delivery, no
live NIC I/O, no timers, no retransmission, no TLS, and no Kestrel claim.

## Files changed

- `src/OS.Kernel/Net/TcpReceiveDispatcher.cs`
- `tests/OS.HostTests/TcpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step619.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Tee-Object -FilePath artifacts\logs\host-tests-tcp-receive-dispatcher.log`

## Evidence

- Build passed with 0 warnings and 0 errors.
- Full host-test execution completed with `TEST_PASS host.tests`.
- New TCP receive markers emitted:
  - `TEST_PASS net.tcp.rx dispatch accepts established payload`
  - `TEST_PASS net.tcp.rx dispatch acknowledges fin`
  - `TEST_PASS net.tcp.rx dispatch handles reset`
  - `TEST_PASS net.tcp.rx dispatch rejects bad checksum`
  - `TEST_PASS net.tcp.rx dispatch reports stream failure`
  - `TEST_PASS net.tcp.rx dispatch rejects non tcp`
  - `TEST_PASS net.tcp.rx dispatch default result is not success`
- Evidence log: `artifacts/logs/host-tests-tcp-receive-dispatcher.log`.

## Failures

- None in the executed host build or host-test run.

## Deferred work

- Wire this dispatcher to a real TCP control-block table after live virtio-net
  RX and connection ownership exist.
- Deliver successful established payloads into PAL/socket receive queues after
  exported socket boundaries and scheduler wakeups are implemented.
- Add retransmission, timers, receive-window handling, route selection, ARP
  integration, and live NIC descriptor ownership.
- Use the dispatcher as one prerequisite for hosted .NET 10/Kestrel, but do not
  claim Kestrel or cloud readiness from this host-only slice.

## Evidence boundary

This is host-tested metadata and caller-buffer evidence only. It does not prove
live virtio-net RX/TX, DHCP/DNS over live packet I/O, TCP sockets, FetchApp,
hosted .NET 10, ASP.NET Minimal API, or Kestrel.

