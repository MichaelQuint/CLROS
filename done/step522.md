# Step 522 - ENA register readiness default guards

## Summary

Implemented Gitea issue #507 by making ENA register aperture validation and
admin readiness result structs reject default/zero values as successful
evidence.

## Why

The ENA register readiness status enums use zero-valued success members.
Default result values previously reported success even though no aperture
validation or readiness assessment had occurred.

## Files changed

- `src/OS.Kernel/Drivers/EnaRegisterMap.cs`
- `tests/OS.HostTests/EnaRegisterMapTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step522.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.ena.registermap rejects default results` and
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

TEST_START drivers.ena.registermap rejects default results
TEST_PASS drivers.ena.registermap rejects default results
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

- This is host-tested ENA register metadata and readiness planning evidence
  only. It does not discover an ENA PCI function, size or map BARs, perform
  live MMIO reads/writes, allocate DMA-safe queues, publish admin queue bases,
  ring admin doorbells, parse admin descriptors, configure interrupts, move
  packets, support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.

