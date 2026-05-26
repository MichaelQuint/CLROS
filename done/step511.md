# Step 511 - Virtio initialization profile default guard

## Summary

Implemented Gitea issue #496 by making
`VirtioDeviceInitializationProfileResult` reject default/zero values as
successful block/network virtio initialization profile evidence.

## Why

`VirtioDeviceInitializationProfileStatus.Success` is zero. The result struct
previously reported success for default values even though no block/network
profile had been selected and no nested initialization plan had been composed.
Profile evidence must only be successful after explicit planner creation.

## Files changed

- `src/OS.Kernel/Drivers/VirtioDeviceInitializationProfile.cs`
- `tests/OS.HostTests/VirtioDeviceInitializationProfileTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step511.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtio.profile default result is not success` and
  `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.virtio.profile default result is not success
TEST_PASS drivers.virtio.profile default result is not success
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

- This is host-tested block/network virtio initialization profile planning
  evidence only. It does not reset a device, write MMIO status bits, read
  feature registers, observe a live `FEATURES_OK` response, select or create
  live queues, allocate DMA-safe memory, notify a device, perform block/network
  I/O, support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.
