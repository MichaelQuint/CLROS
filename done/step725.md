# Step 725 - Shared MAC Validation and HTTP Diagnostic Status Names

## Summary

Implemented Gitea issues #683 through #687 as a networking/driver cleanup slice:

- #683: `MacAddress` now exposes `IsZero` and `IsBroadcast`.
- #684: virtio-net and ENA configuration validation now use the shared MAC helpers.
- #685: ARP cache, IPv4 route planning, UDP transmit, and DNS UDP planning now use the shared MAC helpers where existing status semantics allowed it.
- #686: TCP active, passive, established-stream, and close frame planners now use the shared broadcast helper.
- #687: HTTP GET diagnostic formatters now serialize duplicate response `Content-Length` as `DuplicateContentLength` instead of `Unknown`.

## Why

Network and driver code had repeated zero/broadcast MAC checks spread across packet planners and device configuration parsers. Centralizing those checks keeps link-identity validation consistent before the future live virtio-net and ENA paths consume the same contracts. The duplicate response `Content-Length` parser status added in the previous step also needed formatter coverage so diagnostics remain machine-readable and specific.

## Files changed

- `src/OS.Kernel/Net/MacAddress.cs`
- `tests/OS.HostTests/NetworkPacketTests.cs`
- `src/OS.Kernel/Drivers/VirtioNetDeviceConfig.cs`
- `src/OS.Kernel/Drivers/EnaDeviceAttributes.cs`
- `tests/OS.HostTests/VirtioNetDeviceConfigTests.cs`
- `src/OS.Kernel/Net/ArpNeighborCache.cs`
- `src/OS.Kernel/Net/Ipv4NeighborRoutePlanner.cs`
- `src/OS.Kernel/Net/UdpTransmitPlanner.cs`
- `src/OS.Kernel/Net/DnsResolverTransactionPlanner.cs`
- `tests/OS.HostTests/ArpNeighborCacheTests.cs`
- `src/OS.Kernel/Net/TcpIpv4FramePlanner.cs`
- `src/OS.Kernel/Net/TcpPassiveIpv4FramePlanner.cs`
- `src/OS.Kernel/Net/TcpEstablishedStreamIpv4FramePlanner.cs`
- `src/OS.Kernel/Net/TcpConnectionCloseIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpIpv4FramePlannerTests.cs`
- `src/OS.Kernel/Net/HttpClientGetTransactionDiagnosticFormatter.cs`
- `src/OS.Kernel/Net/HttpTcpClientGetPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/HttpClientGetTransactionTests.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `done/step725.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build log: `artifacts\logs\issues683-687-host-build-20260525.log`
  - Result: `Build succeeded.`
  - Result: `0 Warning(s)`, `0 Error(s)`
- Host test log: `artifacts\logs\issues683-687-hosttests-20260525.log`
  - `TEST_PASS net.macaddress exposes zero broadcast helpers`
  - `TEST_PASS drivers.net mac validation uses shared helpers`
  - `TEST_PASS net.mac validation uses shared helpers`
  - `TEST_PASS net.tcp mac validation uses shared helpers`
  - `TEST_PASS net.http.clientget formatter names duplicate content length`
  - `TEST_PASS net.http.tcp.get formatter names duplicate content length`
  - `TEST_PASS host.tests`
- Whitespace log before this done-step: `artifacts\logs\issues683-687-git-diff-check-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.
- Whitespace log after docs/done update: `artifacts\logs\issues683-687-git-diff-check-postdone-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- `artifacts\logs\issue687-host-build-20260525.log` records an earlier transient output-DLL lock by `OS.HostTests (8232)`.
- The later integrated build and full host-test run passed.
- No real QEMU boot was run for this helper/diagnostic slice; evidence is host-tested only.

## Deferred work

- Live virtio-net and ENA device paths still need real PCI/BAR/MMIO/DMA ownership before these validation helpers can be proven against hardware.
- DHCP, DNS, TCP, FetchApp, PAL sockets, hosted .NET 10, ASP.NET Minimal API, and Kestrel still have no live NIC-backed evidence.
- Duplicate response `Content-Length` diagnostics are now specific, but chunked transfer, close-delimited response bodies, TLS, redirects, and full HTTP semantics remain unsupported.
