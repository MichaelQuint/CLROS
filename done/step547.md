# Step 547 - Virtio-net device-config default-result guards

## Summary

Added explicit initialized-state guards to the virtio-net device configuration
parse and fake register-window read results so default values no longer satisfy
`Succeeded`.

## Why

Both result structs use status enums where `Success` is zero. Without an
initialized guard, a zero/default value could be mistaken for parsed
configuration or completed fake device-configuration register-window evidence.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetDeviceConfig.cs`
- `tests/OS.HostTests/VirtioNetDeviceConfigTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step547.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to virtio-net device-config markers and `TEST_PASS host.tests`.
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
TEST_START drivers.virtionet.config parses mac address
TEST_PASS drivers.virtionet.config parses mac address
TEST_START drivers.virtionet.config parses optional status
TEST_PASS drivers.virtionet.config parses optional status
TEST_START drivers.virtionet.config accepts minimum config
TEST_PASS drivers.virtionet.config accepts minimum config
TEST_START drivers.virtionet.config rejects short config
TEST_PASS drivers.virtionet.config rejects short config
TEST_START drivers.virtionet.config rejects zero mac
TEST_PASS drivers.virtionet.config rejects zero mac
TEST_START drivers.virtionet.config rejects broadcast mac
TEST_PASS drivers.virtionet.config rejects broadcast mac
TEST_START drivers.virtionet.config default results are not success
TEST_PASS drivers.virtionet.config default results are not success
TEST_START drivers.virtionet.config.windowexec reads mac address
TEST_PASS drivers.virtionet.config.windowexec reads mac address
TEST_START drivers.virtionet.config.windowexec reads optional status
TEST_PASS drivers.virtionet.config.windowexec reads optional status
TEST_START drivers.virtionet.config.windowexec rejects short scratch buffer
TEST_PASS drivers.virtionet.config.windowexec rejects short scratch buffer
TEST_START drivers.virtionet.config.windowexec propagates register window failure
TEST_PASS drivers.virtionet.config.windowexec propagates register window failure
TEST_START drivers.virtionet.config.windowexec propagates access failure
TEST_PASS drivers.virtionet.config.windowexec propagates access failure
TEST_START drivers.virtionet.config.windowexec propagates config parse failure
TEST_PASS drivers.virtionet.config.windowexec propagates config parse failure
TEST_START drivers.virtionet.config.windowexec preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.config.windowexec preserves no-live-mmio boundary
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

- This is host-tested parser/fake register-window result contract safety only.
  It does not prove live MMIO, live device reads, feature negotiation, queue
  configuration, interrupts, packet I/O, cloud readiness, Kestrel support, or
  production readiness.

