# Step 520 - Virtio-net TX register-window readiness default guard

## Summary

Implemented Gitea issue #505 by making the register-window virtio-net TX
readiness result reject default/zero values as successful evidence.

## Why

`VirtioNetTransmitRegisterWindowReadinessStatus.Success` is zero, so a default
`VirtioNetTransmitRegisterWindowReadinessResult` previously reported success
even though no queue setup register-window fake execution, descriptor
publication, barrier readiness, notification planning, or notify-window fake
execution had been composed.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitRegisterWindowReadinessPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitRegisterWindowReadinessPlanTests.cs`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step520.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for
  `drivers.virtionet.tx.readiness.windowexec default result is not success`
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

TEST_START drivers.virtionet.tx.readiness.windowexec default result is not success
TEST_PASS drivers.virtionet.tx.readiness.windowexec default result is not success
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

- This is host-tested virtio-net register-window TX readiness composition
  evidence only. It does not map BARs, perform live MMIO, execute real memory
  barriers, notify a live device, allocate or map DMA memory, process
  interrupts, poll hardware, wait for completion, transfer packets, support
  ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove production
  readiness.

