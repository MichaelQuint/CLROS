# Step 546 - Virtio PCI BAR aperture default-result guard

## Summary

Added an explicit initialized-state guard to
`VirtioPciBarApertureValidationResult` so a default value no longer satisfies
`Succeeded` when the status enum defaults to `Success`.

## Why

The BAR aperture validator feeds later virtio PCI binding and register-window
planning. A zero/default result should not be accepted as validated aperture
metadata, because no caller-provided BAR descriptors or sizes were checked.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBarApertureValidator.cs`
- `tests/OS.HostTests/VirtioPciBarApertureValidatorTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step546.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to virtio PCI BAR aperture markers and `TEST_PASS host.tests`.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START drivers.virtio.pci.bar validates required apertures
TEST_PASS drivers.virtio.pci.bar validates required apertures
TEST_START drivers.virtio.pci.bar accepts 64bit memory bar
TEST_PASS drivers.virtio.pci.bar accepts 64bit memory bar
TEST_START drivers.virtio.pci.bar rejects io bar capability
TEST_PASS drivers.virtio.pci.bar rejects io bar capability
TEST_START drivers.virtio.pci.bar rejects absent capability bar
TEST_PASS drivers.virtio.pci.bar rejects absent capability bar
TEST_START drivers.virtio.pci.bar rejects region beyond bar size
TEST_PASS drivers.virtio.pci.bar rejects region beyond bar size
TEST_START drivers.virtio.pci.bar rejects failed discovery input
TEST_PASS drivers.virtio.pci.bar rejects failed discovery input
TEST_START drivers.virtio.pci.bar default result is not success
TEST_PASS drivers.virtio.pci.bar default result is not success
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- None.

## Deferred work

- This is host-tested validation-result contract safety only. It does not prove
  live PCI config access, BAR sizing writes, BAR mapping, MMIO, DMA,
  interrupts, packet/block I/O, cloud readiness, Kestrel support, or production
  readiness.

