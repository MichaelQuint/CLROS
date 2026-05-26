# Step 552 - Virtio-net TX completion default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioNetTransmitCompletionPlanResult` so a default result no longer satisfies
`Succeeded`.

## Why

TX descriptor ownership reclaim depends on a successful TX completion result.
Because the success enum value is zero, a default result could otherwise look
like valid completion/reclaim evidence before any fake-sink or register-window
completion planner had run.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitCompletionPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitCompletionPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step552.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to TX completion markers and `TEST_PASS host.tests`.
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
TEST_START drivers.virtionet.tx.completion accepts matching used entry
TEST_PASS drivers.virtionet.tx.completion accepts matching used entry
TEST_START drivers.virtionet.tx.completion accepts executed notification
TEST_PASS drivers.virtionet.tx.completion accepts executed notification
TEST_START drivers.virtionet.tx.completion advances wrapped used index
TEST_PASS drivers.virtionet.tx.completion advances wrapped used index
TEST_START drivers.virtionet.tx.completion reports no completion
TEST_PASS drivers.virtionet.tx.completion reports no completion
TEST_START drivers.virtionet.tx.completion rejects unexpected used index
TEST_PASS drivers.virtionet.tx.completion rejects unexpected used index
TEST_START drivers.virtionet.tx.completion rejects wrong descriptor head
TEST_PASS drivers.virtionet.tx.completion rejects wrong descriptor head
TEST_START drivers.virtionet.tx.completion rejects unexpected byte count
TEST_PASS drivers.virtionet.tx.completion rejects unexpected byte count
TEST_START drivers.virtionet.tx.completion rejects short queue memory
TEST_PASS drivers.virtionet.tx.completion rejects short queue memory
TEST_START drivers.virtionet.tx.completion rejects failed publish
TEST_PASS drivers.virtionet.tx.completion rejects failed publish
TEST_START drivers.virtionet.tx.completion rejects failed notification
TEST_PASS drivers.virtionet.tx.completion rejects failed notification
TEST_START drivers.virtionet.tx.completion rejects failed notification execution
TEST_PASS drivers.virtionet.tx.completion rejects failed notification execution
TEST_START drivers.virtionet.tx.completion rejects wrong executed notification kind
TEST_PASS drivers.virtionet.tx.completion rejects wrong executed notification kind
TEST_START drivers.virtionet.tx.completion rejects executed notification queue mismatch
TEST_PASS drivers.virtionet.tx.completion rejects executed notification queue mismatch
TEST_START drivers.virtionet.tx.completion rejects executed notification index mismatch
TEST_PASS drivers.virtionet.tx.completion rejects executed notification index mismatch
TEST_START drivers.virtionet.tx.completion accepts register-window notification
TEST_PASS drivers.virtionet.tx.completion accepts register-window notification
TEST_START drivers.virtionet.tx.completion rejects failed register-window notification
TEST_PASS drivers.virtionet.tx.completion rejects failed register-window notification
TEST_START drivers.virtionet.tx.completion rejects wrong register-window notification kind
TEST_PASS drivers.virtionet.tx.completion rejects wrong register-window notification kind
TEST_START drivers.virtionet.tx.completion rejects register-window notification queue mismatch
TEST_PASS drivers.virtionet.tx.completion rejects register-window notification queue mismatch
TEST_START drivers.virtionet.tx.completion rejects register-window notification index mismatch
TEST_PASS drivers.virtionet.tx.completion rejects register-window notification index mismatch
TEST_START drivers.virtionet.tx.completion preserves register-window notification boundary
TEST_PASS drivers.virtionet.tx.completion preserves register-window notification boundary
TEST_START drivers.virtionet.tx.completion preserves no-mmio boundary
TEST_PASS drivers.virtionet.tx.completion preserves no-mmio boundary
TEST_START drivers.virtionet.tx.completion preserves executed-notification boundary
TEST_PASS drivers.virtionet.tx.completion preserves executed-notification boundary
TEST_START drivers.virtionet.tx.completion default result is not success
TEST_PASS drivers.virtionet.tx.completion default result is not success
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

- Claude Code retry failed in noninteractive mode with
  `Not logged in - Please run /login`, so no Claude-authored review or edits
  were used for this slice.

## Deferred work

- This is host-tested TX completion/reclaim contract safety only. It does not
  prove live virtio-net queue notification, packet I/O, interrupts, DMA-safe
  allocation, hardware polling, networking readiness, cloud readiness, Kestrel
  support, or production readiness.

