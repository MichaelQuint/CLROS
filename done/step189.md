# Step 189 - DHCP Lease Configuration Metadata

## Summary

Implemented issue #208 as a host-tested DHCP lease materialization contract. `DhcpLeaseConfiguration` converts a validated `DhcpClientLease` into immutable network configuration metadata without assigning addresses to an interface, creating sockets, scheduling renewal, or transmitting packets.

## Why

The DHCP client transaction can reach a bound lease in host tests, but later networking layers need a stable metadata object that records the selected IPv4 address, optional subnet/router/DNS values, server identifier, lease seconds, and presence flags. This creates that boundary while preserving the current status-coded, caller-owned-buffer style.

## Files Changed

- `src/OS.Kernel/Net/DhcpLeaseConfiguration.cs`
- `tests/OS.HostTests/DhcpLeaseConfigurationTests.cs`
- `docs/networking.md`
- `done/step189.md`

## Tests Run

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

Observed host-test markers:

- `TEST_PASS net.dhcp.lease materializes full config`
- `TEST_PASS net.dhcp.lease materializes partial config`
- `TEST_PASS net.dhcp.lease rejects missing address`
- `TEST_PASS net.dhcp.lease rejects missing server identifier`
- `TEST_PASS net.dhcp.lease rejects zero lease time`
- `TEST_PASS net.dhcp.lease rejects invalid subnet mask`
- `TEST_PASS host.tests`

`dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors. `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`. `git diff --check` exited 0 with CRLF normalization warnings only.

## Failures

None.

## Deferred Work

- Interface address assignment.
- DHCP timers, renew, rebind, and expiry handling.
- Socket creation or packet transmission.
- Live virtio-net DHCP packet I/O.
- ARP conflict detection.
- Kestrel or hosted .NET network integration.
