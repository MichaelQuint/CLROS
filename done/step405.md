# Step 405 - TCP active-connect SYN-ACK checksum validation

## Summary
- Added inbound IPv4 TCP pseudo-header checksum validation to the active-connect
  `TcpIpv4FramePlanner.ReceiveSynAckFrame` path.
- Updated active-connect and HTTP/TCP GET host fixtures so server SYN-ACK frames
  carry valid TCP checksums.
- Added direct and wrapper-level bad SYN-ACK checksum rejection markers.

## Why
The networking stack already had a caller-owned `Ipv4TransportChecksum`
primitive, but active TCP SYN-ACK receive still accepted checksum-zero fixtures.
Validating the checksum before consuming the active-connect state makes the
host-tested TCP frame boundary stricter without claiming live NIC, socket,
or Kestrel behavior.

## Files changed
- `src/OS.Kernel/Net/TcpIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpIpv4FramePlannerTests.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step405.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host-test build passed with 0 warnings and 0 errors.
- Host-test run emitted:
  - `TEST_PASS net.tcp.ipv4.frame accepts syn ack frame`
  - `TEST_PASS net.tcp.ipv4.frame rejects bad tcp checksum`
  - `TEST_PASS net.http.tcp.get rejects bad synack checksum`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0. The command emitted only existing
  LF-to-CRLF working-copy warnings.

## Failures
- None.

## Deferred work
- Passive TCP IPv4 handshake checksum validation.
- Established TCP payload checksum validation.
- UDP receive/transmit checksum enforcement.
- Live virtio-net packet I/O, TCP control blocks, exported PAL sockets,
  hosted .NET 10, Minimal API/Kestrel, and cloud readiness remain unproven.

## Evidence boundary
- This is host-only caller-owned frame validation for active-connect inbound
  SYN-ACK frames. It does not prove outbound TCP checksum generation,
  passive-open checksum validation, established payload checksum validation,
  live NIC packets, sockets, PAL exports, hosted .NET 10, Kestrel,
  cloud readiness, or production readiness.

