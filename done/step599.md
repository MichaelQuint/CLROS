# Step 599 - TCP Established Stream IPv4 Frame Checksum Boundary

## Summary

Added a host-tested established TCP payload frame planner that wraps
`TcpEstablishedStream` send/receive operations in caller-owned
Ethernet/IPv4/TCP frames with IPv4 TCP pseudo-header checksum generation and
validation.

## Why

The stack had established-stream payload primitives and HTTP-specific frame
checksum evidence, but no reusable generic established-payload frame boundary.
This slice moves Kestrel prerequisites forward without claiming sockets, live
virtio-net I/O, timers, retransmission, hosted .NET, or Kestrel support.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStreamIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpEstablishedStreamIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `done/step599.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- Temporary focused `net10.0` console harness outside the repo compiling the
  new `tests/OS.HostTests/TcpEstablishedStreamIpv4FramePlannerTests.cs` source
  and referencing `src/OS.Kernel/OS.Kernel.csproj` plus
  `std/no-runtime/shared/OS.Std.NoRuntime.Shared.csproj`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|net\.tcp\.stream\.ipv4|TEST_PASS host\.tests'`

## Evidence

- Host-test project build passed with 0 warnings and 0 errors.
- Focused temporary harness emitted:
  - `TEST_PASS net.tcp.stream.ipv4 writes payload checksum`
  - `TEST_PASS net.tcp.stream.ipv4 receives checksum-valid payload`
  - `TEST_PASS net.tcp.stream.ipv4 rejects bad checksum`
  - `TEST_PASS net.tcp.stream.ipv4 rejects identity and short output`
  - `TEST_PASS net.tcp.stream.ipv4 default result is not success`

## Failures

- The first host-test build after this slice was blocked by existing
  `OS.HostTests.exe` processes (`PID 6848`, `PID 4376`) holding the output
  executable open. Those stale test runners were stopped and the build then
  passed.
- A later build retry saw leftover handles from `OS.HostTests` (`PID 2508`) and
  a `.NET Host` process (`PID 13788`) after the integrated runner failed early.
  They were gone by inspection before the final build retry, and the final
  host-test project build passed.
- The integrated host-test run exits before reaching the new networking tests
  because pre-existing `ImageBuilderTests` currently fail:
  `imagebuilder plan records default workload profile`,
  `imagebuilder.kestrel_package stages files`,
  `imagebuilder.kestrel_package rejects invalid contract`,
  `imagebuilder.kestrel_package stages published package`,
  `imagebuilder.kestrel_package readback preserves published files`, and
  `imagebuilder.kestrel_package readback preserves manifest json`.
  The failure boundary is outside this networking slice.

## Deferred work

- This does not allocate or manage TCP control blocks.
- This does not implement retransmission, timers, receive-window negotiation,
  route selection, ARP integration, sockets/PAL exports, live virtio-net RX/TX,
  FetchApp, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.
