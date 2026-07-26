# Step 852 - M01-005 Host-Audited Owned Boot-Stack Switch Plan

## Summary

Implemented the host-only M01-005 contract for an audited x64 transition from
the firmware stack to one future CLROS-owned stack. The new leaf:

- Validates one dedicated `LoaderCode` stub page, one exact `LoaderData`
  guard/stack arena, and one canonical `BootStackSwitchStub` reservation.
- Freezes a first-party 14-byte C#-authored switch sequence.
- Emits and verifies those bytes only within the exact planned 4 KiB page and
  at the exact planned entry offset.
- Retains the complete immutable plan in readback evidence so another plan,
  page, buffer, or offset cannot arm the transition.
- Binds one exact, disjoint, caller-attested static 536-byte state range into
  the immutable plan.
- Requires the canonical state range to be exactly zero before
  initialization, then mutates that caller-owned record through a checked
  one-shot state machine.
- Audits the exact Windows-x64 continuation-entry `RSP` and preserves
  distinct failed-audit and synthetic unexpected-return terminal causes.
- Compiles in the retained `NOSTDLIB_EFI`/no-framework-reference closure.

No switch bytes are executed by this step.

## Why

M01-004 defined physical ownership and guard/usable-stack geometry, but it did
not describe a virtual call frame, materialized x64 bytes, a nonreturning
continuation contract, or authoritative per-plan transition state. M01-005
provides those host-audited contracts before later issues link or invoke them
from the no-stdlib EFI artifact.

The design deliberately uses an immutable plan plus one canonical
caller-owned mutable record. This avoids hidden static state and rejects
ordinary plan-copy or alternate-buffer replay while leaving same-address
stale-byte rollback and concurrent serialization to M01-017.

## Files changed

- `src/OS.Kernel/Boot/OwnedBootStackSwitchPlan.cs`
- `tests/OS.HostTests/OwnedBootStackSwitchPlanTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step852.md`

No forbidden integration file, reference snapshot, EFI project file,
`BootSequence.cs`, `Probes.cs`, `README.md`, or completion-gate file changed.

## Tests run

All commands ran from `C:\Projects\CLROS-M01-005` unless stated otherwise.

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
```

Passed with 0 warnings and 0 errors.

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

Passed with 0 warnings and 0 errors.

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Passed with exit code 0. The final unfiltered log contains 5,858 lines and no
`TEST_FAIL` marker. It contains exactly one each of:

```text
TEST_START m01.005
CLROS_M01_005_READY
TEST_PASS m01.005
TEST_PASS host.tests
```

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
```

Passed with 0 warnings and 0 errors.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
```

Passed:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
```

Passed with the `SingleMinimalApi` profile and
`TEST_PASS qemu.runner.dryrun`. This is command/tooling preflight only.

```powershell
git diff --check
git status --short
```

`git diff --check` passed. The pre-commit status contained only the five
declared files above. Git emitted line-ending conversion warnings for the two
existing documentation files; it reported no whitespace error.

## Evidence

Final tool versions:

- .NET SDK: `10.0.302`
- Git: `2.54.0.windows.1`
- Windows PowerShell: `5.1.26100.8894`
- Branch: `codex/M01-005-owned-stack-switch-plan`
- Base commit: `5b5ca0035fca75edb0205d7edaa384bfaf241137`

Final production and test source hashes:

- `OwnedBootStackSwitchPlan.cs`:
  `6DA09C3C187AF6C37BB243D483D73EF4FB83DD5A8395FAB3CE68928F02F2734A`
- `OwnedBootStackSwitchPlanTests.cs`:
  `F6E95AFD1CCFB57F9DFA59EA45E66321D0DCE6199B197F716511C19453D041B1`
- `docs/boot.md`:
  `9B2082B4E011AD1C778FE0674FB04B9F6A52FF39A0E7A420AEFCE089F2B2D068`
- `docs/architecture.md`:
  `D77DC39740B7D696C6060BA66D44FFF7BF5198A367591C3ED37B2337CC2A2CFE`

The final unfiltered host log was generated at
`2026-07-26T05:16:04.1280277Z`:

- Development-host path:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-005-host-final-20260726-011157.log`
- SHA-256:
  `F9DA5FB49CB2545ADCA59ACD84431CCDC7ED3A6D68B226A19BB3788563E66FE2`
- Bytes: `749078`
- Lines: `5858`
- `TEST_FAIL` count: `0`

The fixed byte plan is:

```text
48 89 CC       mov rsp, rcx
48 31 ED       xor rbp,rbp
6A 00          push 0
48 89 E1       mov rcx,rsp
41 FF E0       jmp r8
```

For usable-stack end `E`, the plan derives:

```text
pre-transfer RSP       = E - 32
continuation-entry RSP = E - 40
fake return slot       = [E - 40, E - 32)
32-byte home space     = [E - 32, E)
```

The exact no-stdlib compile fixture includes the typed-address source,
canonical reservation source and validator, M01-004 layout,
`UefiMemoryType`, `BootMemoryRegionMaterializer`, the M01-005 leaf, and a
positive pointer/page/state probe. It defines `NOSTDLIB_EFI`, removes
framework references, and verifies zero assembly references in the emitted
PE.

The implementation was independently authored after reviewing these
owner-authorized SharpOS snapshot paths from `C:\Projects\CLROS\references`:

- `SharpOS-main/OS/src/Boot/BootStackSwitch.cs`:
  `637DCFC91E3B9647A0F8FBD4800E6C973ED78C6E5F73E2F7C10770C000234808`
- `SharpOS-main/OS/src/Boot/BootStackSwitchStub.cs`:
  `82BD871E197F5666E97B7A6124E360E9FDFBAD478E8579CBA2993700E96A3B02`
- `SharpOS-main/OS/src/Boot/BootStackSwitchPatcher.cs`:
  `4D889735C1507FB7F4DE53E616AB96929F49348531559BFD967A14F585063315`
- `SharpOS-main/OS/src/Boot/BootStackPool.cs`:
  `B7C80FDD76BB0D17CF87AB358CCC50F413601D8A1B8C0A6ABB22EE8D40D90E7C`
- `SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`:
  `B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500`

No source was copied wholesale. CLROS does not import SharpOS mutable global
state, its fixed stack pool, raw-pointer API, in-place method-body patching,
BootAsm/Iced dependency, or writable-executable assumption.

## Failures

- The first draft stored transition state in a readonly copied plan. Strict
  review demonstrated that replaying the original `Planned` value could arm
  twice. The final design moved transition authority into one canonical
  address-bound mutable record.
- The first draft emitted and verified a detached 14-byte buffer. Strict
  review demonstrated that evidence from another buffer or plan could arm the
  transition. The final design binds the complete 4 KiB page, pointer, offset,
  lengths, and complete immutable plan snapshot.
- The first state draft treated any invalid embedded plan as empty and did
  not reserve one canonical state range. The final initializer checks all 536
  bytes for zero, and the plan rejects state overlap with code, guard, stack,
  handoff, and continuation.
- The first terminal model conflated a failed RSP audit with a repeated
  unexpected return. The final record persists and revalidates the terminal
  cause and exact status.
- Early development harness runs were stopped after code changed underneath
  their compiled assemblies. They were not used as evidence. The final
  ordinary unfiltered run above passed from the final compiled source.
- Documentation initially implied that return-to-zero necessarily faults.
  It now states that this depends on the later page-zero-unmapped policy.

## Deferred work

This step proves only a host-audited switch-byte plan, exact materialization
and readback contract, caller-serialized one-shot state, and RSP-containment
audit.

It does not:

- Execute `mov rsp`, jump to or call the continuation, or alter processor
  state.
- Prove that the handoff/state/continuation caller attestations are live.
- Allocate or map the code/stack/state pages.
- Apply RW-to-RX protection or enforce W^X/NX.
- Link this leaf into `src/OS/OS.csproj` or call it from `EfiEntry`.
- Prevent a privileged caller from restoring stale bytes at the same
  canonical state address.
- Observe a continuation return; `ReportUnexpectedReturn` is a synthetic
  host classifier, and the emitted fake return targets zero.
- Run under QEMU/OVMF or provide guest/hardware evidence.
- Change `docs/completion-gates.md` or close any live boot/paging gate.

M01-017 owns authoritative cross-leaf serialization, M01-020 owns explicit EFI
linkage, M01-021 owns live allocation/wiring, and M01-022 owns real-QEMU
evidence.
