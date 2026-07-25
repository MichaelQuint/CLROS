# Step 521 - Virtio-net TX register-window reclaim default guard

## Summary

Implemented Gitea issue #506 by making the register-window virtio-net TX
completion/reclaim result reject default/zero values as successful evidence.

## Why

`VirtioNetTransmitRegisterWindowReclaimStatus.Success` is zero, so a default
`VirtioNetTransmitRegisterWindowReclaimResult` previously reported success even
though no successful readiness, used-ring completion validation, or descriptor
ownership reclaim had been composed.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitRegisterWindowReclaimPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitRegisterWindowReclaimPlanTests.cs`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step521.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for
  `drivers.virtionet.tx.reclaim.windowexec default result is not success`
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

TEST_START drivers.virtionet.tx.reclaim.windowexec default result is not success
TEST_PASS drivers.virtionet.tx.reclaim.windowexec default result is not success
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

- This is host-tested virtio-net register-window TX completion/reclaim
  composition evidence only. It does not wait for a live device, process
  interrupts, own a runtime descriptor allocator, perform live MMIO, transfer
  packets, support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.

