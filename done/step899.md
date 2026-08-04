# Step 899 - Bind checked In8 and Out8 unmanaged function pointers

## Summary

Implemented M02-003's production, value-only checked binder for the exact
unmanaged `Out8` and `In8` signatures. The binder independently revalidates the
callable plan, payload bounds, address/offset identity, non-overlap, byte
counts, and COM1 register window before casting an address. Its invocation
methods reject ports outside `0x3f8` through `0x3ff` before transfer and return
status-coded results after the unmanaged target returns.

Host tests execute safe `[UnmanagedCallersOnly]` callbacks and prove the port,
byte, and return-value flow. The separately owned EFI integration compiles the
same binder into the no-stdlib closure and supplies the required real-QEMU
readiness marker without invoking the generated privileged offsets.

## Why

Earlier M02 slices planned the exact instruction bytes, materialized the fixed
page, and safely executed offset zero. They did not provide a production
checked function-pointer boundary. M02-003 closes that bounded software
contract while keeping pointer validation, safe host invocation, guest address
readiness, and future privileged device I/O as distinct evidence layers.

## Files changed

M02-003 owns exactly:

- `src/OS.Kernel/Diagnostics/InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs`
- `tests/OS.HostTests/InvokeIn8Out8CheckedUnmanagedFunctionPointersTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- this uniquely reserved `done/step899.md`

Production implementation commit
`bef76b95a9fe5a60effc3446ea8d1040ecfa8d1b` added the binder, host tests, and
initial documentation. No-stdlib follow-ups
`8eee1a9e5fc7239341917ccbe80d4e2bda2add7f` and
`37b35fcd4b955ce6d2d883a6aa3a96ab8f74a343` retained the same owned production
source. The complete source/evidence candidate is
`84032f4b78804194d3352410a1f2749112d94e5f`, tree
`a02bbe8b8ccf6ce7cd13b8957dd081d5476130b3`.

Shared EFI files and their tests belong to supplemental issue #438 and are
recorded separately in step 900. The user-requested README checkpoint is a
separate commit and is not attributed to M02-003.

## Tests run

Accepted frozen-source commands include:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -RequireMarker "CLROS_M02_003_READY"
git diff --check
git status --short
```

The focused production runner also passed all eight cases and emitted:

```text
TEST_START m02.003
TEST_PASS m02.003 binds and invokes checked unmanaged out8 and in8 targets
TEST_PASS m02.003 rejects an unready binding plan before invocation
TEST_PASS m02.003 rejects an out8 target whose checked offset mismatches
TEST_PASS m02.003 rejects a callable range outside the checked payload
TEST_PASS m02.003 rejects overlapping checked callable ranges
TEST_PASS m02.003 rejects ports outside the configured com1 register window
TEST_PASS m02.003 default results cannot invoke unmanaged targets
TEST_PASS m02.003 results contain no managed references
CLROS_M02_003_READY
TEST_PASS m02.003
```

## Evidence

Accepted evidence root:

```text
C:\Projects\CLROS-M02-003\artifacts\logs\m02-003-final\84032f4-20260804T052701Z-r4
```

The fresh r4 host wave passed from the clean frozen commit:

| Command | Exit | Seconds | stderr bytes | stdout SHA-256 |
|---|---:|---:|---:|---|
| solution build | 0 | 22.914 | 0 | `35FCA1A816C31B7168078D02C0F59382011EEECDCA3D3EA0FD202900C7469E9F` |
| HostTests build | 0 | 4.964 | 0 | `0C5336F5E63851A9468483C8A67E24D9A106D038889F83DDEE2707EFDE3A746A` |
| complete HostTests | 0 | 2516.192 | 0 | `ABDF130B9FD907F3B8E23C463DC409C95FC46B11FC0C3ED9784624F953848BD0` |
| naming/source firewall | 0 | 37.774 | 0 | `FB9E2F9DEB178474FD6713AA598348A053B28FC79CA27732159CDD0AC565CE18` |
| default QEMU dry-run | 0 | 2.166 | 0 | `884F7AC3833B695CBD983AAF4A19FC57E6E75403A3026A98D4B83A3DDDAD6A65` |
| all-switch EFI build | 0 | 41.046 | 0 | `15A8182E8EF601C2B5D0F00CD11124F43ED63E6392C3FB8A20DBC0BB86075FCC` |
| exact issue QEMU command | 0 | 9.043 | 0 | `E170C48055CB2D9FBA75C72F5777AD8770C196ED7F615FE8837411E501DA10D0` |
| `git diff --check` | 0 | 0.041 | 0 | empty SHA-256 |
| clean status | 0 | 0.044 | 0 | empty SHA-256 |

Complete HostTests recorded 3,365 `TEST_START`, 3,366 `TEST_PASS`, zero
`TEST_FAIL`, exactly one `TEST_PASS host.tests`, and exactly one M02-003 start,
ready, and pass marker. The additional pass is the terminal suite marker; raw
counts include marker-bearing nested contract fixtures.

The exact real-QEMU issue command observed `CLROS_M02_003_READY` once and no
`TEST_FAIL`. The stronger shared-lane profile independently required the full
payload, ordering, uniqueness, and privileged-I/O exclusions. Its public
serial SHA-256 is
`CC1DDEBBD8C5355D9C318BADE9F7778349D4EF6949EFA1160E40A304263AADCC`.
The EFI SHA-256 is
`C7666C1580A1C362DB3165166B005478910C5F3FD53C4F451E1A8F4EB57CE794`.

## Failures

- An early no-stdlib build rejected `ushort.MaxValue`; commit `37b35fc`
  replaced that unavailable BCL constant with the bounded literal `0xffffu`
  without changing behavior.
- Final attempt r1 was rejected when inherited M01-020 tests still expected the
  old 48-item closure. Attempt r2 was rejected when the M02-017 prerequisite
  guard counted the downstream M02-003 dependency; commit `84032f4` restored
  the prerequisite ownership check.
- Attempt r3 completed the test body with zero failures but its ignored wrapper
  recorded a blank process exit code. It wrote `HOST_GATE_FAIL` and was not
  promoted. r4 captured native exit zero and `HOST_WAVE_PASS`.

All rejected logs remain in their original evidence roots and contribute no
acceptance proof.

## Deferred work

- Independent review, push, PR merge, tracker evidence marker, issue closure,
  and backlog synchronization remain required after this documentation-only
  ledger commit.
- Guest offsets 16 and 32 were checked and bound but not invoked.
- Live device I/O, packet loss, runtime unwind, cloud cleanup, COM1 ownership,
  post-`ExitBootServices` diagnostics, descriptor/fault installation, M02 exit,
  provider support, hosted .NET, Kestrel, CloudReady status, and production
  readiness remain unproven.

The evidence boundary is exactly the production checked binder, safe unmanaged
host calls, and the separately integrated guest readiness marker. It is not a
live privileged-I/O result.
