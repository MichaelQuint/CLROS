# Step 512 - Virtio descriptor catalog default guard

## Summary

Implemented Gitea issue #497 by making
`VirtioDeviceDescriptorCatalogResult` reject default/zero values as successful
virtio device descriptor catalog evidence.

## Why

`VirtioDeviceDescriptorCatalogStatus.Success` is zero. The result struct
previously reported success for default values even though no direct or
fake-derived binding provenance, device-configuration parse, initialization
profile, or descriptor publication had been composed. Descriptor catalog
evidence must only be successful after explicit builder creation.

## Files changed

- `src/OS.Kernel/Drivers/VirtioDeviceDescriptorCatalog.cs`
- `tests/OS.HostTests/VirtioDeviceDescriptorCatalogTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step512.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtio.descriptor.catalog.derive default result is not success`
  and `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.virtio.descriptor.catalog.derive default result is not success
TEST_PASS drivers.virtio.descriptor.catalog.derive default result is not success
TEST_PASS host.tests

dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall

git diff --check
exit 0; existing LF-to-CRLF working-copy warnings only
```

## Failures

- None.

## Deferred work

- This is host-tested virtio descriptor metadata evidence only. It does not read
  live device configuration, stabilize config generation, write status
  registers, negotiate features with hardware, configure queues, bind drivers,
  transfer packets, read sectors, support ASP.NET Minimal API/Kestrel, prove
  cloud readiness, or prove production readiness.
