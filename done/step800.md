# Step 800 - ICMP Receive Dispatcher

## Summary

Added a host-tested ICMP receive dispatcher for caller-owned virtio-net RX
buffers. The dispatcher composes existing virtio-net/Ethernet/IPv4 network
dispatch metadata with `IcmpEchoPacket` parsing so future diagnostic ping work
has a status-coded receive boundary before live virtio-net packet I/O exists.

## Why

Parent issue #6 needs networking progress in small code-bearing slices. ICMP
echo packet/frame helpers already existed, but protocol-specific receive
dispatch stopped at `NetworkReceiveDispatcher` and UDP. This step adds the
same style of ICMP echo receive metadata boundary without introducing live NIC
I/O, socket delivery, timers, or Kestrel claims.

## Files changed

- `src/OS.Kernel/Net/IcmpReceiveDispatcher.cs`
- `src/OS.Kernel/Net/Ipv4Header.cs`
- `src/OS.Kernel/Net/Ipv4IcmpEchoFrame.cs`
- `tests/OS.HostTests/IcmpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step800.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
  - Log: `artifacts/logs/issue758-host-build.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: pass
  - Log: `artifacts/logs/issue758-host-tests.log`
- `git diff --check -- src/OS.Kernel/Net/IcmpReceiveDispatcher.cs src/OS.Kernel/Net/Ipv4Header.cs src/OS.Kernel/Net/Ipv4IcmpEchoFrame.cs tests/OS.HostTests/IcmpReceiveDispatcherTests.cs docs/networking.md docs/completion-gates.md done/step800.md`
  - Result: pass
  - Log: `artifacts/logs/issue758-diff-check.log`
  - Notes: log contains existing Git line-ending normalization warnings only.

## Evidence

- `TEST_PASS done.step.allocate`
- `TEST_PASS net.icmp.rx parses echo request`
- `TEST_PASS net.icmp.rx parses echo reply`
- `TEST_PASS net.icmp.rx reports classifier failure`
- `TEST_PASS net.icmp.rx rejects non-ipv4 payload`
- `TEST_PASS net.icmp.rx rejects non-icmp protocol`
- `TEST_PASS net.icmp.rx rejects truncated icmp packet`
- `TEST_PASS net.icmp.rx rejects bad checksum`
- `TEST_PASS net.icmp.rx default result is not success`
- `TEST_PASS host.tests`

## Failures

None.

## Deferred work

This is host-tested caller-owned receive-buffer metadata only. It does not
prove live virtio-net RX/TX, interrupt processing, packet I/O, ARP routing,
diagnostic ping sessions, sockets, DHCP/DNS/TCP, hosted .NET, ASP.NET Minimal
API, Kestrel, or cloud-readiness behavior.
