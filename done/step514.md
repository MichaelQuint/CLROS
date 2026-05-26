# Step 514 - Virtio PCI ISR read-plan default guard

## Summary

Implemented Gitea issue #499 by making
`VirtioPciIsrReadPlanResult` reject default/zero values as successful ISR
read-plan evidence.

## Why

`VirtioPciIsrReadPlanStatus.Success` is zero. The result struct previously
reported success for default values even though no ISR aperture validation or
read-plan creation had happened. ISR read-plan evidence must only be successful
after explicit parser/factory initialization.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciIsrStatus.cs`
- `tests/OS.HostTests/VirtioPciIsrStatusTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step514.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtiopci.isr default read plan result is not success`
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

TEST_START drivers.virtiopci.isr default read plan result is not success
TEST_PASS drivers.virtiopci.isr default read plan result is not success
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

- This is host-tested virtio PCI ISR metadata/read-plan evidence only. It does
  not configure MSI-X, APIC, or IOAPIC, perform live MMIO ISR reads, drain
  queues, dispatch packets, support ASP.NET Minimal API/Kestrel, prove cloud
  readiness, or prove production readiness.

