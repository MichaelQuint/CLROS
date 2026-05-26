# Step 199 - DNS Resolver Transaction Planner

## What Changed

- Added `OS.Net.DnsResolverTransactionPlanner` as a caller-driven DNS-over-UDP transaction planner.
- Added `DnsResolverTransactionPlannerInfo` for local interface, next-hop MAC, DNS server IPv4, client port, transaction ID, and IPv4 identification metadata.
- Added `DnsResolverTransactionPlanResult` and `DnsResolverTransactionPlanStatus` so DNS query/response handling reports deterministic status codes instead of throwing for expected packet/protocol failures.
- Added host tests with `net.dns.client.transaction ...` markers for query creation, successful A response, wrong transaction ID, wrong DNS server IP, wrong UDP ports, NXDOMAIN/no-answer, malformed DNS payload, and unsupported answer type.

## Why It Changed

The network stack needs a layer above `DnsUdpFrame` that preserves resolver transaction metadata and validates that a received UDP DNS response belongs to the configured DNS server before a future socket, retry, cache, or live NIC path consumes it.

## Files Changed

- `src/OS.Kernel/Net/DnsResolverTransactionPlanner.cs`
- `tests/OS.HostTests/DnsResolverTransactionPlannerTests.cs`
- `done/step199.md`

## Tests Run

```text
dotnet build .\OS.slnx --nologo -m:1
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

```text
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

DNS evidence before the later unrelated failure:

```text
TEST_PASS net.dns.client.transaction writes query frame
TEST_PASS net.dns.client.transaction resolves matching response
TEST_PASS net.dns.client.transaction rejects wrong transaction
TEST_PASS net.dns.client.transaction rejects wrong server
TEST_PASS net.dns.client.transaction rejects wrong udp ports
TEST_PASS net.dns.client.transaction reports nxdomain and no answer
TEST_PASS net.dns.client.transaction reports malformed response
TEST_PASS net.dns.client.transaction reports unsupported answer type
```

## Failures

The full host harness exited nonzero after the DNS planner markers passed because `drivers.virtionet.rx.lifecycle reports completion failures` failed in `tests/OS.HostTests/VirtioNetReceiveLifecycleTests.cs`. That file is outside this worker's ownership for issue #218 and was not modified here.

## Deferred Work

- No live DNS, sockets, retries, timers, route selection, DNS cache, CNAME, AAAA, TXT, or multiple-answer selection is implemented.
- TTL is preserved for the first accepted A answer as diagnostic metadata only; no cache lifetime behavior consumes it yet.
- Shared networking documentation still needs parent integration because this worker was asked not to edit shared docs.

## Evidence Boundary

This step proves only caller-owned-buffer DNS transaction planning over existing UDP/DNS helpers. It is not evidence of virtio-net packet I/O, DHCP-applied DNS configuration, live DNS resolution, FetchApp behavior, hosted .NET 10 networking, ASP.NET Minimal API, or Kestrel support.
