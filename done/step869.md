# Step 869 - M01-021 opt-in loaded-image and owned-stack EFI adapter

## Summary

Completed Gitea issue M01-021's source/build boundary. An explicit
`M01_LOADED_IMAGE_STACK_PROBE` build now wires the host-tested loaded-image,
reservation-ledger, and owned-stack stage into the no-stdlib `EfiEntry` path
without changing the ordinary all-false boot path.

The opt-in adapter:

- captures the running EFI image through the loaded-image protocol;
- allocates one `EfiLoaderCode` page and a separate 34-page
  `EfiLoaderData` guard/stack arena;
- recaptures the memory map after both allocations and constructs the exact
  four-record reservation ledger;
- validates a 3,264-byte caller-owned static evidence scratch aggregate and
  returns only an 8-byte status/checkpoint result on the firmware stack;
- fills and verifies the complete code page with `0xCC`, then copies and reads
  back disjoint 14-byte stack-switch and 27-byte stack-probe plans;
- installs a fail-closed `RhpStackProbe` bridge before the large preparation
  call, clears and verifies the probe bytes before any code-page cleanup, and
  makes the owned-stack continuation a direct unmanaged nonreturning boundary;
- reports success/checkpoint markers only after the continuation observes an
  `RSP` inside the 33-page usable-stack range; and
- preserves exact firmware status and deterministic reverse-order cleanup on
  every pre-transfer failure.

The NativeAOT helper ledger now carries `RhpStackProbe` as a dedicated
append-only EFI-only, non-completing helper area. It remains distinct from the
four reverse-P/Invoke exports and twelve fail-fast type-cast exports.

## Why

M01-017 had a complete host-tested stage-one contract, and M01-020 made its
source closure explicit in the EFI project, but nothing invoked that contract
from `EfiEntry`. The first rooted implementation also exposed a 135,632-byte
nested pre-transfer frame chain, which exceeded the UEFI x64 128-KiB minimum
before caller, marker, firmware-call, and interrupt overhead.

M01-021 crosses only the next source/build boundary. The shared coordinator now
has one low-stack core used by both the compact live adapter and the existing
rich host-evidence wrapper. The accepted generated object reduces the deepest
adapter-owned pre-transfer chain to 41,576 bytes and leaves 23,960 bytes below
the issue's 64-KiB half-stack ceiling.

The page-probe algorithm was independently encoded as first-party C# byte data
after reviewing the MIT-licensed .NET runtime v10.0.8 x64 special ABI at:

`https://github.com/dotnet/runtime/blob/v10.0.8/src/coreclr/nativeaot/Runtime/amd64/MiscStubs.asm`

No upstream assembly source was copied or added to the first-party tree.

## Files changed

- `.gitea/backlog/clros-issues.json`
  - records the final M01-021 ownership, helper-ledger obligation, acceptance
    criteria, exact done-step path, and evidence boundary.
- `src/OS/Boot/EfiEntry.cs`
  - adds the single opt-in loaded-image, allocation, recapture, byte-plan,
    cleanup, continuation, and marker adapter.
- `src/OS/Runtime/MinimalRuntime.cs`
  - adds the minimal compiler-recognized `UnmanagedCallersOnlyAttribute`
    contract and fail-closed `RhpStackProbe` dispatch bridge.
- `src/OS.Kernel/Boot/M01LoadedImageStackIntegration.cs`
  - adds the shared low-stack core, caller-owned static scratch validation,
    compact result, overflow/alias/capacity preflight, and rich host wrapper.
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
  - appends `EfiStackProbe` and `EfiOnlyStackProbe` without renumbering existing
    values and records the exact non-completion boundary.
- `tests/OS.HostTests/M01LoadedImageStackEfiEntryTests.cs`
  - adds focused source, mutation, byte-plan, failure, cleanup, marker, and
    stack-probe-install-before-large-prepare contract tests.
- `tests/OS.HostTests/M01LoadedImageStackIntegrationTests.cs`
  - covers compact static-scratch success, default/zero/short/oversized/
    misaligned/nonzero/outside/alias/overflow rejection, nonmutation, and the
    exact single shared-core call topology used by both public preparation
    entrypoints.
- `tests/OS.HostTests/BootSequencePlanTests.cs`
  - narrowly replaces the historical source-absence predicate with the exact
    opt-in adapter contract while retaining the default-off boundary.
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
  - verifies all 17 exports exactly once, the new single-symbol stack-probe
    category, and stable append-only enum values.
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
  - locks the complete M01-021 owned-path set and collision lane.
- `docs/boot.md`
- `docs/efi-build.md`
- `docs/nativeaot-nostdlib-limits.md`
  - document the accepted source/build/PE/frame evidence and explicit nonclaims.
- `done/step869.md`
  - preserves this evidence.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - passed with zero warnings and zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - passed with zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - passed the complete harness.
  - exact terminal marker: `TEST_PASS host.tests`.
  - no `TEST_FAIL` line was present.
  - focused markers included `TEST_START m01.021`,
    `CLROS_M01_021_READY`, and `TEST_PASS m01.021`.
  - accepted log:
    `artifacts/m01-021-authoritative-audit-20260727-061009.log`
  - process exit code: `0`.
  - stdout size: 406,123 bytes across 6,388 lines.
  - log SHA-256:
    `B69347A99E027CD4C736B85179322761CD9E3228FAB196C8A73E7CC0CBB2B193`.
  - stderr was empty; the empty-file SHA-256 was
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe`
  - passed `TEST_PASS efi.artifact.verify` and
    `TEST_PASS efi.shell.publish`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - passed `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - passed `TEST_PASS qemu.runner.dryrun`; this is command validation only.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -Apply`
  - converged with `SYNC_VERIFY_PASS milestones=16 labels=68 issues=332`
    after each issue-scope expansion.
- `git diff --check`
  - passed.
- First-party native-source scan under `src`, `std`, and `apps`
  - found zero `.c`, `.cpp`, `.h`, `.asm`, or `.s` files.

## Evidence

The accepted opt-in artifact is a 131,584-byte PE32+ AMD64 EFI application.
The artifact verifier reported EFI application subsystem 10, executable
file-backed `.managed` entrypoint RVA `0x00010BD0`, same-build `EfiMain`
association, and absent ordinary, bound, IAT, and delay-import directories.

Accepted hashes:

- `artifacts/efi/BOOTX64.EFI`:
  `42DC4647AC3EC1B0CAF2A86863834C186E8A0E324DBEE37B879FC401F1E7930F`
- `src/OS/obj/Release/net10.0/win-x64/native/OS.obj`:
  `D3E31151A51ABE783F2638674C8A7A897BDE8A4064A04FA9D9E29446896F8F22`
- `artifacts/efi/BOOTX64.EFI.link.map`:
  `81BABEFFD51033B42014AD5356C11F00A81C503BE1BA50EB668EDD0903AE8323`
- `artifacts/efi/BOOTX64.EFI.link.rsp`:
  `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F`

Toolchain evidence:

- MSVC linker `14.51.36252.0`
- Windows SDK `10.0.28000.0`
- ILCompiler package `10.0.8`, file `10.0.826.23019`
- NativeAOT runtime pack `10.0.8`
- QEMU `11.0.50`
- OVMF 3,653,632 bytes,
  SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`

The final generated-object frame audit measured:

| Nested call | Generated contribution | Cumulative |
| --- | ---: | ---: |
| `Run` | `0x410` | `0x410` |
| `PrepareAndTransfer` | `0x10A0` | `0x14B0` |
| `PrepareFromStaticScratch` | `0x280` | `0x1730` |
| shared low-stack core | `0x1610` | `0x2D40` |
| `OwnedBootStackSwitchPlan.Create` | `0x7360` | `0xA0A0` |
| switch-plan `Validate` | `0x120` | `0xA1C0` |
| layout `Validate` | `0x60` | `0xA220` |
| `BootReservedRange.Validate` | `0x40` | `0xA260` |
| `ValidateKind` return address | `0x8` | `0xA268` |

`0xA268` is 41,576 bytes. The remaining margin below 64 KiB is
`0x5D98`, or 23,960 bytes. The accepted generated RVAs were:

- `Run`: `0x182E0`
- `PrepareAndTransfer`: `0x19610`
- `PrepareFromStaticScratch`: `0xD930`
- shared low-stack core: `0xDB70`
- `OwnedBootStackSwitchPlan.Create`: `0x8450`
- switch-plan `Validate`: `0xB720`
- layout `Validate`: `0x7A30`
- `BootReservedRange.Validate`: `0x1440`
- `ValidateKind`: `0x15B0`

This generated-object inspection is a mandatory manual acceptance gate tied to
the exact `OS.obj` hash above; it is not performed by the focused host test.
`CLROS_M01_021_READY` alone is therefore not frame-budget or emitted-ABI
evidence, and no changed source or toolchain output may inherit this audit.

The emitted `RhpStackProbe` bridge begins at RVA `0x1140`, ends at
`0x115D`, and has zero unwind codes. It loads and tests the static probe target
through `RAX`, selects the compiler's encoded-target branch when target bit 1
is set or the direct branch for the accepted 16-byte-aligned target, tail-jumps
through
`RAX`, and loops on itself while no target is published. The accepted direct
path does not write `RSP`, `R11`, or argument registers before dispatch. A
generated call-graph audit found no post-unpublication path back to
`RhpStackProbe` before code-page cleanup.

Independent read-only production, generated-object, test, and diff reviews
found no remaining actionable correctness issue before acceptance.

## Failures

- The first rooted adapter produced a 135,632-byte nested pre-transfer frame
  chain. It was rejected and replaced by the caller-owned static-scratch core;
  no oversized artifact was accepted.
- A focused mutation initially changed an unrelated `while` loop and therefore
  did not prove the intended bridge failure. The mutation now targets the
  bridge method specifically.
- One synthetic data-alias fixture overlapped its synthetic code reservation
  and failed earlier with `AllocationTableRejected`. The code record was moved
  to a disjoint page so the test reaches and proves the intended scratch/data
  alias rejection.
- The first full post-integration harness run found 17 EFI runtime exports but
  only 16 ledger symbols. M01-021's owned scope was expanded before the fix;
  `RhpStackProbe` now has a dedicated append-only ledger entry and exact tests.
- Final adversarial review showed the first ledger check was one-directional:
  a duplicate source export could replace a missing one while the count stayed
  17. The accepted test compares both source and EFI-ledger symbols, rejects
  duplicates on either side, and requires 17 unique exact matches.
- Final test-design review showed the shared-core relationship and mandatory
  probe-install-before-large-frame order were not independently locked. Source
  topology and call-order mutation tests now reject either regression.
- The same adversarial review then exposed focused-test gaps around run-local
  map-recapture ordering, the success-line writer, allocation and overlap
  validation, static-scratch address overflow, the exact stack-probe byte
  result, and the no-write interval before installation. Exact-body and
  targeted mutation checks now reject each regression.
- The first attempted final evidence run outlived its timed launcher and lost
  both redirected output and a retrievable exit code. Its zero-byte logs were
  rejected rather than treated as evidence; the harness was relaunched through
  a native process handle with live file redirection.
- No secret, forbidden first-party native source, generated artifact, or
  reference implementation was added to the commit.

## Deferred work

This step proves source wiring, host behavior, the generated-object frame and
bridge shape, and a fresh canonical EFI build only.

It does **not** prove that the loaded-image protocol or allocations succeed in
a guest, the post-allocation map is recaptured from firmware, either byte plan
executes, `RSP` changes or remains contained, the reserved guard page is
unmapped or faults, the code page transitions RW to RX, NX/W^X is enforced,
execution survives `ExitBootServices`, or any completion gate advances.

M01-022 exclusively owns real QEMU/OVMF execution, serial marker ordering,
live-`RSP` containment, timeout/failure classification, and preserved evidence.
It was not claimed or started by this step.
