# Step 536 - Virtio PCI Discovery Default Result Guard

## Summary
- Added explicit initialization metadata to `VirtioPciDeviceDiscoveryResult`.
- Updated `VirtioPciDeviceDiscoveryResult.Succeeded` so default/zero discovery
  results no longer report success before the discovery contract creates a
  result.
- Added host marker coverage and updated the driver and completion-gate docs.

## Why
`VirtioPciDeviceDiscoveryStatus.Success` is the zero enum value. Without an
initialization guard, `default(VirtioPciDeviceDiscoveryResult)` can be mistaken
for successful virtio PCI discovery evidence even though no PCI identity or
capability metadata was validated.

## Files changed
- `src/OS.Kernel/Drivers/VirtioPciDeviceDiscovery.cs`
- `tests/OS.HostTests/VirtioPciDeviceDiscoveryTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step536.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtiopci default result is not success`,
  `TEST_PASS host.tests`, and failures.
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
TEST_START drivers.virtiopci default result is not success
TEST_PASS drivers.virtiopci default result is not success
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
- None for this slice.

## Deferred work
- This is host-tested caller-provided virtio PCI metadata evidence only.
- No live PCI enumeration, ECAM mapping, BAR mapping, feature negotiation,
  queue setup, driver binding, block/network I/O, cloud readiness, or
  production readiness is claimed.

