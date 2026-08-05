# Step 902 - Add the checked COM1 owned port-I/O backend

## Summary

Implemented M02-004's production C# backend over the reviewed checked unmanaged
function-pointer binding. The backend exposes status-coded byte reads/writes,
confines access to COM1 ports `0x3F8..0x3FF`, executes the exact seven-write
115200 8N1 initialization plan, and provides adapters for the existing serial
writer and line-status read path.

## Why

M02-003 proved checked callable addresses without invoking guest port I/O.
M02-004 required the next bounded behavior: one concrete owned backend that
could configure COM1 through that binding while failing closed for unready
bindings, invalid ports, default results, and incomplete calls.

## Files changed

- `src/OS.Kernel/Diagnostics/COM1OwnedPortIOBackend.cs`
- `tests/OS.HostTests/COM1OwnedPortIOBackendTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`

Shared EFI/project/script integration belongs to supplemental issue #441 and
is recorded separately in `done/step904.md`. The prerequisite instruction-order
repair belongs to #442 and is recorded in `done/step903.md`.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
  - Exit `0`; zero warnings and zero errors after the repair wave converged.
- Complete HostTests acceptance wave
  - Final exact counts and stream hashes are posted to Gitea against the pushed
    review head; the development run that exposed three stale byte consumers is
    recorded under Failures and was not accepted.
- Real QEMU/OVMF M02-004 profile
  - Exit `0`; `TEST_PASS qemu.smoke` and
    `TEST_PASS m01.026.raw-observations`.
- `git diff --check`
  - Exit `0` before documentation/ledger finalization.

## Evidence

Host tests emit exactly:

```text
TEST_START m02.004
CLROS_M02_004_READY configurationWrites=7 hostCallbacks=true guestPortIo=false
TEST_PASS m02.004
```

The production tests prove the exact seven configuration calls, existing
serial-writer adaptation, line-status reads, unready-binding rejection,
out-of-COM1-range rejection, and value-only/default-fail-closed results.

The separately claimed EFI integration produced exactly one guest marker:

```text
CLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false
```

The 485,888-byte EFI artifact SHA-256 is
`61CA5D6879DA1AE7DC0A6938AA61AD64DCEFBD61AA937C0DF2D8C3BF0809820E`.
The 2,753-byte serial log SHA-256 is
`E452EF725FAF34775EE00C310E86245F217CA2638926B70B3194C711A0F2876D`.
All 50 cumulative profile markers were ordered and unique; zero configured
forbidden markers appeared.

## Failures

- A first integration attempt added a static value result in the
  layout-sensitive EFI path and stopped at `M01_PRE_CR3`. That attempt was
  rejected; the result was changed to a local value after the inherited M01
  restoration checks.
- A second real-QEMU attempt reached `M01_PAGE_ZERO_ABSENT` and stopped on the
  first `Out8`. Inspection proved the accepted stub overwrote `DL` while moving
  `CX` into `DX`. No guest evidence from that attempt was promoted.
- The first complete host run after the repair started 470 groups, passed 467,
  failed 3, and exited `-532462766`. It revealed a stale exact-order validator
  in `Com1PortIoBackendPlan` plus an old independent page image in the M02 EFI
  tests. Issue #442 ownership was expanded before those consumers were edited.
- The next complete host attempt outlived its obsolete 30-minute orchestration
  wrapper while still progressing through `test-efi-artifact.ps1`. The wrapper
  and its verified CLROS-only child process tree were stopped; the incomplete
  stream was rejected rather than treated as a test failure or pass.
- A later complete host attempt was intentionally terminated when the user
  requested a reboot. The surviving CLROS-only child process tree was stopped
  and verified absent before restart; that incomplete stream was not promoted.

## Deferred work

This step proves a status-coded production backend and, through #441/#442, the
seven pre-`ExitBootServices` guest configuration writes. It does not prove
`In8` polling, owned serial payload transport, post-`ExitBootServices`
diagnostics, the M02 exit criterion, physical hardware/provider behavior,
hosted .NET, Minimal API, Kestrel, CloudReady status, or production readiness.
