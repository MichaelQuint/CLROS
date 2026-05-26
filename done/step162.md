# Step 162 - ARP Neighbor Resolution Transaction

## What Changed

- Added `ArpNeighborResolutionTransaction` as a deterministic one-target ARP resolver over caller-owned Ethernet frames.
- Added status-coded transitions for `Initial -> RequestSent -> Resolved` and explicit failed timeout/rejection paths.
- Added focused host tests for request-frame writing, matching reply acceptance, mismatched/malformed reply rejection, invalid state handling, too-small request buffers, and caller-driven timeout.
- Updated networking and completion-gate documentation to reflect the new host-tested boundary.

## Why It Changed

Gitea issue #181 needs the next networking primitive between raw ARP packet encode/decode and future virtio-net packet I/O. This keeps neighbor resolution deterministic and testable before there is a live NIC, neighbor cache, retransmission timer, or packet scheduler.

## Files Changed

- `src/OS.Kernel/Net/ArpNeighborResolutionTransaction.cs`
- `tests/OS.HostTests/ArpNeighborResolutionTransactionTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step162.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\artifacts\tmp\issue181\focused\Issue181Focused.csproj
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue181-default.log
git diff --check -- .\src\OS.Kernel\Net\ArpNeighborResolutionTransaction.cs .\tests\OS.HostTests\ArpNeighborResolutionTransactionTests.cs .\docs\networking.md .\docs\completion-gates.md .\done\step162.md
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` first hit a transient output lock from an existing `OS.HostTests` process, then passed with 0 warnings and 0 errors after that process exited.
- Focused ignored verification harness `artifacts/tmp/issue181/focused` exited `FOCUSED_TEST_EXIT 0` and emitted:

```text
TEST_PASS net.arp resolver writes request frame
TEST_PASS net.arp resolver accepts matching reply
TEST_PASS net.arp resolver rejects mismatched reply
TEST_PASS net.arp resolver times out request
```

- The standard default-output no-build host harness also emitted all four issue #181 markers before failing later in an unrelated virtio PCI capability parser test.
- Scoped `git diff --check` exited 0 with only existing LF-to-CRLF normalization warnings for docs files.

## Failures

- The first default `tests/OS.HostTests` build failed because another `OS.HostTests` process held `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`; retrying after that process exited passed.
- A full standard no-build host harness run failed after the ARP resolver markers on `drivers.virtiopci.capability parser rejects truncated capability` with `ArgumentOutOfRangeException`. That failure is outside the issue #181 files and was not changed in this slice.

## Deferred Work

- No neighbor cache table is implemented.
- No live virtio-net RX/TX path is implemented.
- No retransmission timer or retry policy is implemented.
- No ARP conflict detection, duplicate handling, or gratuitous ARP support is implemented.

## Evidence Boundary

This step proves only a host-tested, one-target ARP request/reply transaction over caller-owned frames. It does not prove live NIC packet I/O, packet scheduling, virtio-net queue setup, DHCP integration, IP routing, TCP connectivity, FetchApp, or Kestrel behavior.
