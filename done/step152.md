# Step 152 - IPv4 ICMP Echo Frame Planning Contract

## What Changed

- Added `OS.Net.Ipv4IcmpEchoFrame` and `Ipv4IcmpEchoFrameInfo` as a caller-buffer Ethernet + IPv4 + ICMPv4 echo frame compose/parse contract.
- Added focused host tests for deterministic echo request frame writing, echo reply parsing, bad IPv4 checksum rejection, and truncated IPv4 payload rejection.
- Updated networking and completion-gate documentation with the issue #174 evidence boundary.

## Why It Changed

Gitea issue #174 needs a small planning contract that can compose and parse complete Ethernet/IPv4/ICMP echo frames using the existing packet primitives before virtio-net RX/TX, ARP resolution, timers, or live packet I/O exist.

## Files Changed

- `src/OS.Kernel/Net/Ipv4IcmpEchoFrame.cs`
- `tests/OS.HostTests/Ipv4IcmpEchoFrameTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step152.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --artifacts-path C:\Projects\OS\artifacts\tmp\issue174-final
dotnet run --project .\artifacts\tmp\issue174-focused\Issue174FocusedTests.csproj --artifacts-path C:\Projects\OS\artifacts\tmp\issue174-focused-final
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --artifacts-path C:\Projects\OS\artifacts\tmp\issue174-final` passed with 0 warnings and 0 errors.
- Focused issue #174 host-test harness emitted:
  - `TEST_PASS net.icmp.ipv4 frame writes echo request`
  - `TEST_PASS net.icmp.ipv4 frame parses echo reply`
  - `TEST_PASS net.icmp.ipv4 frame rejects bad ip checksum`
  - `TEST_PASS net.icmp.ipv4 frame rejects truncated payload`

## Failures

- A normal test-project build against the default `tests/OS.HostTests\bin` output was blocked by an existing long-running `OS.HostTests.exe` process locking output files. The successful rerun used `--artifacts-path` to avoid disturbing that process.
- A first isolated build attempt used one shared intermediate output directory for multiple project references and failed with duplicate generated assembly attributes. The successful rerun used the SDK artifact layout.

## Deferred Work

- No virtio-net RX/TX queues are implemented.
- No NIC-backed Ethernet send/receive exists.
- No ARP resolution, echo retry, session tracking, or timeout scheduling is implemented.
- No live ICMP dispatch or routing is implemented.

## Evidence Boundary

This step is host-tested frame planning only. It proves deterministic caller-buffer Ethernet + IPv4 + ICMP echo request/reply frame encode/parse behavior and status-coded malformed-frame rejection. It does not prove live network I/O, virtio-net behavior, DHCP/DNS integration, TCP, FetchApp, hosted .NET 10, or Kestrel support.
