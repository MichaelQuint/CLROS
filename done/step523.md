# Step 523 - NVMe register aperture default guard

## Summary

Implemented Gitea issue #508 by making the NVMe register aperture validation
result reject default/zero values as successful evidence.

## Why

`NvmeRegisterApertureValidationStatus.Success` is zero, so a default
`NvmeRegisterApertureValidationResult` previously reported success even though
no admin queue register aperture validation had occurred.

## Files changed

- `src/OS.Kernel/Drivers/NvmeRegisterMap.cs`
- `tests/OS.HostTests/NvmeRegisterMapTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step523.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.nvme.registermap rejects default aperture result` and
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

TEST_START drivers.nvme.registermap rejects default aperture result
TEST_PASS drivers.nvme.registermap rejects default aperture result
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

- This is host-tested NVMe controller/admin register metadata and aperture
  validation evidence only. It does not discover a PCI NVMe controller, size or
  map BARs, perform live MMIO reads/writes, allocate DMA queues, ring
  doorbells, poll completions, read blocks, support ASP.NET Minimal API/Kestrel,
  prove cloud readiness, or prove production readiness.

