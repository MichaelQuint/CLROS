# Step 441 - IPv4 checksum result default guard

## Summary

Implemented Gitea issue #426 by making the IPv4 transport checksum result
reject default/zero values as successful checksum evidence.

## Why

`Ipv4TransportChecksumStatus.Success` is zero.
`default(Ipv4TransportChecksumResult)` previously reported success because
`Succeeded` only checked the status field. UDP, TCP, HTTP, and future socket
paths must not treat zero/default checksum metadata as initialized computation
or validation evidence.

## Files changed

- `src/OS.Kernel/Net/Ipv4TransportChecksum.cs`
- `tests/OS.HostTests/Ipv4TransportChecksumTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step441.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.transport\.checksum default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- IPv4 transport checksum default-result marker evidence:

```text
TEST_START net.transport.checksum default result is not success
TEST_PASS net.transport.checksum default result is not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

## Failures

- None.

## Deferred work

Other networking result structs still need separate default-result hardening
slices. This step is host-tested IPv4 transport checksum contract evidence
only; it does not implement live packet I/O, live HTTP GET, hosted .NET,
Kestrel, cloud readiness, or production readiness.
