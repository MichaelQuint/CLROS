# Step 454 - Virtio-net RX register-window completion default guard

## Summary

Implemented Gitea issue #439 by making
`VirtioNetReceiveRegisterWindowCompletionResult` reject default/zero values as
successful RX register-window completion evidence.

## Why

`VirtioNetReceiveRegisterWindowCompletionStatus.Success` is zero. The
completion result previously reported success for
`default(VirtioNetReceiveRegisterWindowCompletionResult)` because `Succeeded`
only checked status. This result bridges RX readiness, caller-owned used-ring
completion, and frame classification metadata, so uninitialized completion
state must not count as a successful receive boundary.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveRegisterWindowCompletionPlan.cs`
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowCompletionPlanTests.cs`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step454.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.rx\.completion\.windowexec default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Virtio-net RX register-window completion default-result marker evidence:

```text
TEST_START drivers.virtionet.rx.completion.windowexec default result is not success
TEST_PASS drivers.virtionet.rx.completion.windowexec default result is not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

- `git diff --check` exited 0. Git reported existing LF-to-CRLF working-copy
  warnings only.

## Failures

- None.

## Deferred work

- This is host-tested virtio-net RX completion/classification metadata only. It
  does not notify a live device, wait for hardware, perform DMA or live MMIO,
  move packets, prove networking/cloud readiness, host .NET 10, run ASP.NET
  Minimal API, run Kestrel, or prove production readiness.
