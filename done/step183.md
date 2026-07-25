# Step 183 - Issue #202 Network Receive Dispatcher

## Summary

Added the `OS.Net.NetworkReceiveDispatcher` host-test slice for caller-owned virtio-net RX buffers.

## Why

The existing virtio-net RX classifier proved only no-offload virtio header parsing and Ethernet EtherType classification. The next receive-boundary step needs deterministic ARP and IPv4 payload metadata without taking ownership of packet memory, mutating ARP cache state, routing packets, or claiming live NIC behavior.

## Files changed

- `src/OS.Kernel/Net/NetworkReceiveDispatcher.cs`
- `tests/OS.HostTests/NetworkReceiveDispatcherTests.cs`
- `docs/networking.md`
- `done/step183.md`

## Implemented behavior

- Consumes `VirtioNetReceiveFrameClassifier.Classify`.
- Preserves nested classifier status and classifier `PacketParseStatus`.
- Returns the Ethernet header, payload kind, payload offset, and payload length from the classifier result.
- Parses ARP payloads into `ArpPacket` metadata.
- Parses IPv4 payloads into `Ipv4Header` metadata plus IPv4 payload offset/length.
- Reports classifier failures, ARP parse failures, IPv4 parse failures, and unsupported payloads with explicit `NetworkReceiveDispatchStatus` values.

## Tests run

```text
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
Build succeeded. 0 Warning(s), 0 Error(s)

.\scripts\check-naming.ps1
TEST_PASS naming.guard
TEST_PASS source.firewall

git diff --check
Exit code 0. CRLF normalization warnings only.
```

## Requested verification blocked

```text
dotnet build .\OS.slnx --nologo -m:1
Build failed in tests/OS.HostTests/VirtioPciCandidateSelectorTests.cs.
```

The failing file is outside issue #202 ownership. It currently calls `CreateDevice` without the required `deviceId` parameter at multiple call sites. Because `OS.HostTests` cannot rebuild, `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` would use stale binaries and is not valid evidence for the new #202 markers yet.

## Expected markers once the unrelated build blocker is resolved

```text
TEST_PASS net.rx.dispatch parses arp payload
TEST_PASS net.rx.dispatch parses ipv4 payload
TEST_PASS net.rx.dispatch reports classifier failure
TEST_PASS net.rx.dispatch reports arp parse failure
TEST_PASS net.rx.dispatch reports ipv4 parse failure
TEST_PASS net.rx.dispatch reports unsupported payload
```

## Failures

- Full solution build is blocked by unrelated `VirtioPciCandidateSelectorTests.cs` compile errors.

## Deferred work

- No allocation in the dispatcher path.
- No ARP cache update from receive dispatch.
- No TCP, UDP, DHCP, or ICMP routing.
- No live virtio-net queue reads, interrupts, descriptor ownership, DHCP/TCP/Kestrel evidence, or network device I/O claims.
