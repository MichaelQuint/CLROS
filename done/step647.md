# Step 647 - TCP receive-dispatch control metadata guard

## Summary

Hardened `TcpReceiveDispatcher` so caller-owned receive buffers with parsed,
checksummed TCP control segments are validated for port, sequence,
acknowledgement, flag, and payload metadata before the dispatcher accepts an
empty ACK or routes FIN/RST into close-state handling.

## Why

The receive-dispatch slice already rejected bad TCP checksums before mutating
stream or close state, and downstream payload/FIN/RST helpers had their own
validation. The ACK-only dispatcher path, however, could return success for an
empty ACK without checking whether its endpoint or sequence/acknowledgement
metadata matched the established connection. This step adds a small
status-coded guard at the dispatcher boundary so malformed or out-of-window
ACK/FIN/RST metadata is rejected before state mutation.

## Files changed

- `src/OS.Kernel/Net/TcpReceiveDispatcher.cs`
  - Added `TcpReceiveDispatchStatus.ControlSegmentRejected`.
  - Added a control-segment validation gate for empty ACK, FIN, and RST
    receive metadata after TCP checksum validation and before any stream/close
    state mutation.
- `tests/OS.HostTests/TcpReceiveDispatcherTests.cs`
  - Added host markers for valid empty ACK dispatch and rejected
    out-of-window/malformed ACK/FIN/RST control metadata.
- `docs/networking.md`
  - Documented the new host-tested control-metadata rejection boundary and kept
    the evidence scoped to caller-owned buffers, not live virtio-net or socket
    delivery.
- `done/step647.md`
  - Records this evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Full host-test run emitted:
  - `TEST_PASS net.tcp.rx dispatch accepts established payload`
  - `TEST_PASS net.tcp.rx dispatch acknowledges fin`
  - `TEST_PASS net.tcp.rx dispatch handles reset`
  - `TEST_PASS net.tcp.rx dispatch accepts empty ack`
  - `TEST_PASS net.tcp.rx dispatch rejects out-of-window control`
  - `TEST_PASS net.tcp.rx dispatch rejects bad checksum`
  - `TEST_PASS net.tcp.rx dispatch reports stream failure`
  - `TEST_PASS net.tcp.rx dispatch rejects non tcp`
  - `TEST_PASS net.tcp.rx dispatch default result is not success`
  - `TEST_PASS host.tests`

## Failures

None in the focused build or full host-test run.

## Deferred work

This remains host-tested receive-dispatch evidence over caller-owned buffers and
synthetic parsed network metadata only. It does not implement live virtio-net
RX, socket delivery, exported PAL socket receive, retransmission, receive
windows, timers, TLS, hosted .NET, ASP.NET Minimal API, Kestrel, cloud
networking, or production readiness.
