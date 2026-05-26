# Step 525 - ENA device-attributes default guard

## Summary

Implemented Gitea issue #510 by making the ENA device-attributes parse result
reject default/zero values as successful evidence.

## Why

`EnaDeviceAttributesStatus.Success` is zero, so a default
`EnaDeviceAttributesParseResult` previously reported success even though no
caller-buffer response parsing had occurred.

## Files changed

- `src/OS.Kernel/Drivers/EnaDeviceAttributes.cs`
- `tests/OS.HostTests/EnaDeviceAttributesTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step525.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.ena.deviceattr rejects default parse result` and
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

TEST_START drivers.ena.deviceattr rejects default parse result
TEST_PASS drivers.ena.deviceattr rejects default parse result
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

- This is host-tested caller-buffer ENA device-attributes parsing evidence only.
  It does not submit ENA admin commands, allocate DMA memory, ring doorbells,
  poll completions, read BARs, retrieve a live MAC address, move packets,
  support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.

