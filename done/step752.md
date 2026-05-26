# Step 752 - TCP Close IPv4 Frame Diagnostic Marker

## Summary
Added a caller-owned diagnostic formatter for `TcpConnectionCloseIpv4FramePlanResult`.
The formatter emits a stable `TCP_CLOSE_IPV4_FRAME status=` marker with the
active-close frame status, nested close/packet status, byte ranges, ports, TTL,
IPv4 identification, and local/remote sequence numbers.

## Why
FetchApp and future TCP smoke paths need parseable close-frame diagnostics
before live virtio-net, timers, retransmission, or socket ownership exists.
This step makes the already host-tested active FIN/ACK frame metadata visible in
a no-allocation marker format that can be reused by later boot/readiness
plumbing.

## Files changed
- `src/OS.Kernel/Net/TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.cs`
  adds the formatter, status enum, result struct, and required-byte query path.
- `tests/OS.HostTests/TcpConnectionCloseIpv4FramePlannerTests.cs` covers
  successful marker formatting, short-destination reporting, default-result
  rejection, and no-managed-reference result metadata.
- `docs/networking.md` documents the marker surface, tests, and non-live
  networking boundary.
- `done/step752.md` records this evidence.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue710-tcp-close-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue710-tcp-close-formatter\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence
- Build log:
  `artifacts/logs/issue710-host-build-20260525.log`
- Host-test log:
  `artifacts/logs/issue710-hosttests-20260525.log`
- Focused markers:
  - `TEST_PASS net.tcp.close.ipv4.formatter formats active close marker`
  - `TEST_PASS net.tcp.close.ipv4.formatter reports short destination`
  - `TEST_PASS net.tcp.close.ipv4.formatter rejects default result`
  - `TEST_PASS host.tests`

## Failures
None in the focused build or full host-test harness.

## Deferred work
- Wire the close-frame marker into the FetchApp exchange readiness path once the
  exchange path consumes close diagnostics directly.
- Add live virtio-net TX/RX, timers, retransmission, TCP control-block
  ownership, sockets/PAL handoff, and QEMU network evidence in later issues.
- Keep TLS, hosted .NET 10, ASP.NET Minimal API, and Kestrel claims blocked
  until the corresponding gates have live evidence.

## Evidence boundary
This is host-tested diagnostic formatting over an already-produced active-close
FIN/ACK frame plan. It does not perform live packet I/O, publish NIC
descriptors, run DNS/ARP, open sockets, retransmit, use timers, execute
FetchApp, or prove hosted .NET/Kestrel behavior.

