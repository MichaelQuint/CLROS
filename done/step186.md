# Step 186 - Virtio-net device config parser

## Summary

Implemented issue #205 as a host-test-only virtio-net device configuration parser. The new parser reads caller-provided config bytes, extracts the device MAC address, optionally extracts the little-endian status field, and rejects invalid MAC identities before future live virtio-net binding accepts device metadata.

## Why

The virtio-net driver path needs a small deterministic boundary for device configuration bytes before live MMIO reads, feature negotiation, queue setup, or packet I/O. Keeping this as a pure parser makes it testable on the host and keeps the future device driver from accepting unusable all-zero or broadcast MAC identities.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetDeviceConfig.cs`
- `tests/OS.HostTests/VirtioNetDeviceConfigTests.cs`
- `docs/networking.md`
- `done/step186.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and ended with `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` passed with `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed CRLF normalization warnings for existing concurrently edited text files only.

## Evidence

Observed host-test markers:

```text
TEST_PASS drivers.virtionet.config parses mac address
TEST_PASS drivers.virtionet.config parses optional status
TEST_PASS drivers.virtionet.config accepts minimum config
TEST_PASS drivers.virtionet.config rejects short config
TEST_PASS drivers.virtionet.config rejects zero mac
TEST_PASS drivers.virtionet.config rejects broadcast mac
```

## Failures

None known at implementation time.

## Deferred work

- No MMIO/device config reads.
- No live virtio-net PCI binding.
- No feature negotiation or queue setup.
- No interface assignment, DHCP, TCP, hosted .NET, Minimal API, or Kestrel claim.
