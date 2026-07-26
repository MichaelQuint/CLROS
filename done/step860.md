# Step 860 - Host-Audited Reversible CR3 Probe Plan

## Summary

Implemented Gitea issue `M01-013` as three no-stdlib-compatible,
host-audited contracts for a future, explicitly opted-in, reversible CLROS
page-table probe:

1. `X64PagingModePreflightStubPlan` emits and validates a fixed 111-byte,
   32-instruction x64 observation stream and evaluates caller-owned CR0, raw
   CR3, raw CR4, IA32_EFER, CPUID address-width, and entry-RFLAGS evidence.
2. `X64ReversibleCr3TransitionStubPlan` emits and validates a fixed
   1,981-byte, 485-instruction x64 transition/recovery stream. It saves exact
   entry RFLAGS and raw firmware CR3/CR4, disables maskable interrupts, clears
   and verifies CR4.PGE, enters a distinct owned root, updates a continuation
   canary, restores and verifies the exact firmware root while PGE remains
   clear, validates the canary through the restored root, restores CR4, and
   restores exact entry RFLAGS before every return.
3. `X64Cr3LoadStubPlan` binds those exact byte/evidence results to the M01-012
   identity-map result, the M01-005 owned-stack switch result, two explicit
   firmware allocation records, exact modeled state/canary placement, one-shot
   policy, callback-free event order, terminal classification, and pre/post
   marker rules.

The composite is disabled by default and requires an explicit one-shot opt-in
plus a nonzero timeout. It accepts no boolean-only readiness substitutions:
successful sibling materialization, readback, architectural preflight,
transition evidence, address binding, mapping evidence, allocation geometry,
stack geometry, and event transcript must all agree.

The source remains absent from the canonical EFI compile closure. No host test
invokes the emitted bytes or executes `CLI`, `MOV CR3`, `MOV CR4`, `RDMSR`, or
another privileged instruction.

## Why

M01-012 froze the identity ranges and fresh-table capacity needed to keep the
boot image, owned stack, handoff, diagnostics, ACPI scratch, framebuffer
metadata, transition code, and transition data reachable. The next safety
boundary is not permanent page-table adoption; it is a bounded experiment that
can always return to the exact firmware hierarchy and diagnostics context.

That experiment needs three independently reviewable layers:

- A read-only processor-mode observation before any control-register write.
- An exact, finite transition/recovery byte stream whose every return restores
  caller flags and whose post-owned paths restore processor state in one order.
- A composite planner that proves the byte streams, storage, mappings,
  allocations, owned stack, raw register values, canary, event order, and
  timeout policy describe the same attempt.

Keeping those layers host-audited and unlinked lets CLROS reject malformed or
internally inconsistent inputs before M01-019 coordinates live table
construction and invocation. It also prevents a host marker from being
misrepresented as guest, QEMU, or completion-gate evidence.

## Files changed

- `src/OS.Kernel/Memory/X64PagingModePreflightStubPlan.cs`
  - Adds exact C# byte materialization/readback, independent semantic
    instruction metadata, architectural observation/request/result PODs, and a
    bounded pointer/count/capacity evaluator.
  - Requires CR0.PG/WP, CR4.PAE, EFER.LME/LMA/NXE, four-level non-PCID mode,
    architectural RFLAGS bit 1, a 36-52-bit physical width, valid raw firmware
    CR3 bits, and a complete aligned owned-root page inside its table arena.
  - Revalidates every success-POD numeric field, exact page capacity, entry
    offset/address recovery, overflow, and entry containment.
- `src/OS.Kernel/Memory/X64ReversibleCr3TransitionStubPlan.cs`
  - Adds the exact relocation-free transition/recovery stream, materialization
    and readback results, runtime state, modeled-address-bound evidence result,
    terminal evaluator, and structural safety helpers.
  - Validates low-canonical, overflow-safe, mutually disjoint runtime-state,
    canary, and stack spans before its first state write.
  - Rejects zero or unchanged canaries, same-root no-op transitions, PCIDE,
    LA57, callback evidence, stale one-shot state, and malformed state.
  - Restores captured entry RFLAGS on the null-state path, every pre-CLI
    failure, and the common post-CLI recovery path.
- `src/OS.Kernel/Memory/X64Cr3LoadStubPlan.cs`
  - Adds the default-off composite, storage and event PODs, pointer-based
    factory, deep plan/result validity checks, and transcript auditor.
  - Requires a distinct one-page `LoaderCode` allocation and exact
    `LoaderData` table/state arena, and rejects all four overlaps with the
    M01-005 stack-switch code/data allocations.
  - Persists and revalidates the transition evidence's modeled state address;
    it must equal the storage layout's exact state address.
  - Requires RX transition code, RW+NX table/state and stack mappings, exact
    sibling byte capacity/count/entry evidence, stack headroom/caller frame,
    distinct roots, canary agreement, and nine ordered transcript events.
- `tests/OS.HostTests/X64PagingModePreflightStubPlanTests.cs`
  - Adds exact enum/POD/offset, byte/hash/semantic, materialization, readback,
    architectural, pointer, alias, hostile numeric, no-stdlib, evaluated-EFI
    exclusion, and failure-atomicity tests.
- `tests/OS.HostTests/X64ReversibleCr3TransitionStubPlanTests.cs`
  - Adds eleven focused groups covering every public value and offset, exact
    byte/hash decode, all branch targets, ABI/register rules, materialization,
    malformed bytes, live-state semantics, terminal failures, hostile PODs,
    all-return RFLAGS restoration, recovery ordering, source firewall, and
    zero-reference compilation.
- `tests/OS.HostTests/X64Cr3LoadStubPlanTests.cs`
  - Adds the sole M01-013 aggregate coordinator plus composite layout,
    construction, mapping, allocation, sibling evidence, state-address binding,
    transcript, timeout/reset/triple-fault, hostile-copy, source, marker, and
    zero-reference tests.
- `docs/boot.md`
  - Records the exact M01-013 allocation, mapping, byte-stream, processor-state,
    storage, marker, provenance, test, and evidence-boundary contracts.
- `docs/architecture.md`
  - Records the host-tested capability, unsupported live behavior, test
    coverage, and later integration owners without changing a gate.
- `done/step860.md`
  - This evidence record.

No EFI entry, EFI project, boot sequence, diagnostics probe, README,
completion-gate, backlog manifest, synchronization script, or reference file
changed.

## Public contract

The exact public POD sizes are:

```text
X64PagingModePreflightInstruction               24
X64PagingModePreflightObservation               56
X64PagingModePreflightRequest                   32
X64PagingModePreflightResult                    96
X64PagingModePreflightStubResult                56

X64ReversibleCr3TransitionRuntimeState          184
X64ReversibleCr3TransitionStubPlanResult         72
X64ReversibleCr3TransitionEvidenceResult        216

X64Cr3ProbePolicy                                16
X64Cr3ProbeStorageLayout                         56
X64Cr3ProbeEvent                                 40
X64Cr3LoadStubPlan                             1016
X64Cr3LoadStubPlanResult                       1072
X64Cr3ProbeAuditResult                           64
```

Every enum is byte-backed and append-only; zero is never success. The composite
status added during final safety review is
`StackSwitchAllocationOverlap = 72`.

### Preflight stream

The exact preflight identity is:

```text
RequiredByteLength = 111
InstructionCount   = 32
SHA-256             = 71993B5134F7A4936718FD406EBF68CDA7E82476EE15B3F9824A5B6600FF9171
```

Its Windows-x64 entry receives one aligned 56-byte observation record. MOV
stores clear completion/reserved fields without changing flags, PUSHFQ captures
entry RFLAGS, and the stream records raw CR0, CR3, CR4, IA32_EFER, CPUID
`0x80000000`, and CPUID `0x80000008`. The `PLF1` completion signature is the
last observation write. The stream writes no control register and calls no
function.

The evaluator accepts only:

- CR0.PG and CR0.WP set.
- CR4.PAE set, with CR4.LA57 and CR4.PCIDE clear.
- IA32_EFER.LME, LMA, and NXE set.
- CPUID address-size leaf present and physical width from 36 through 52.
- Architectural RFLAGS bit 1 set.
- Raw firmware CR3 containing only its aligned address plus PWT/PCD low bits.
- A nonzero 4 KiB-aligned owned root whose complete page is inside a nonempty,
  aligned table arena and within the selected physical width.

### Transition stream

The exact transition identity is:

```text
RequiredByteLength       = 1981
AuditedInstructionCount  = 485
SHA-256                   = F1D94771DE8C8436CCDA1358A15F51DDE683AC3C77CBB0DC4A85B084888CA484
```

It contains no call, indirect control transfer, STI, external patch,
nonvolatile-GPR write, or XMM6-XMM15 use. It uses only volatile
RAX/RCX/RDX/R8/R9/R10/R11 plus balanced eight-byte pushes/pops, retains caller
shadow space, requires Win64 entry alignment, and returns its append-only
runtime status in EAX.

The exact safety order is:

1. Capture entry RFLAGS before a flag-changing instruction.
2. Validate caller state, one-shot fields, modeled low-canonical storage,
   state/canary/stack disjointness, nonzero canaries, owned-root geometry, and
   entry RSP before the first state write.
3. Record entry state, execute CLI, and require only `IF = 0` plus
   architectural RFLAGS bit 1. Arithmetic/status flags may differ after the
   validation instructions.
4. Capture raw CR3/CR4, reject LA57/PCIDE, and reject an owned root equal to the
   masked firmware root.
5. Clear/read CR4.PGE, load/read owned CR3, and update/read the canary.
6. Restore/read exact raw firmware CR3 with PGE clear, then read the replacement
   canary through the restored root.
7. Restore/read exact raw CR4, restore/read exact full entry RFLAGS, verify
   entry/exit RSP equality, write terminal status, restore entry RFLAGS a final
   time, and use only a flag-neutral MOV before RET.

All 51 reachable returns restore the captured entry RFLAGS. Every path after
the owned CR3 write crosses firmware-CR3 restoration; every path after the PGE
write crosses CR4 restoration. Firmware-CR3, CR4, RFLAGS, and RSP restoration
failures override an earlier operational status. A post-restoration canary
mismatch replaces only an otherwise-successful status, preserving an earlier
useful failure.

### Composite and transcript

The code allocation is exactly one `LoaderCode` page. The data allocation is
exactly the M01-012 table-page count plus one `LoaderData` state page. The first
table page is the owned PML4. The last data page starts with the 184-byte state
record and carries a later aligned 8-byte canary. Probe code/data allocations
must be disjoint from one another and from both M01-005 allocations.

The modeled state address in the transition evidence must equal the storage
layout address. Success evidence from another span, entry, capacity, byte
count, instruction count, mapping, stack plan, root, or state page cannot be
rebound.

The successful transcript contains exactly:

```text
PreMarkerEmitted
TransitionEntered
OwnedRootLoaded
OwnedRootReadBack
CanaryUpdated
FirmwareRootRestored
FirmwareRootReadBack
CanaryValidated
PostMarkerEmitted
```

`M01_POST_CR3` is accepted only after exact raw firmware-root readback and
canary validation. Callback evidence, interrupt re-enable, early/missing
markers, root mismatch, RSP escape, unchanged/corrupt canary, elapsed-time
regression, timeout overflow, timeout, reset, triple fault, or any extra or
reordered event fails deterministically.

## Tests run

All final commands ran from `C:\Projects\CLROS-M01-013` on 2026-07-26 with
.NET SDK `10.0.302`, branch
`codex/M01-013-reversible-cr3-probe`, and base commit
`71cc4bb2faf403b5c3f07969f7cf600ab1deffbb`.

### Production, host-test, and complete solution builds

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

Both final commands exited `0` with `0` warnings and `0` errors.

### Focused leaf tests

Independent isolated runs reached:

```text
TEST_PASS m01.013.01.load
TEST_PASS m01.013.01.load.nostdlib
TEST_PASS m01.013.02.preflight
TEST_PASS m01.013.03.transition.01
TEST_PASS m01.013.03.transition.02
TEST_PASS m01.013.03.transition.03
TEST_PASS m01.013.03.transition.04
TEST_PASS m01.013.03.transition.05
TEST_PASS m01.013.03.transition.06
TEST_PASS m01.013.03.transition.07
TEST_PASS m01.013.03.transition.08
TEST_PASS m01.013.03.transition.09
TEST_PASS m01.013.03.transition.10
TEST_PASS m01.013.03.transition.11
TEST_PASS m01.013.03.transition
```

The preflight and transition focused suites include their exact
`NOSTDLIB_EFI`, `NoStdLib=true`, zero-framework-reference, zero-assembly-
reference compile closures. The aggregate composite suite additionally proves
the exact union of M01-001, M01-005, M01-011, M01-012, and M01-013 production
sources compiles under that boundary.

### Independent Capstone audit

The ordinary-data output from the final transition materializer was decoded
independently with Capstone 5.0.9:

```text
CAPSTONE_PASS bytes=1981 instructions=485 direct_branches=80 reachable=485
ABI_PASS rets=51 pushfq=3 popfq=52 cli=1 sti=0 nonvolatile_writes=0 xmm6_15=0
CONTROL_PASS cr3_reads=3 cr3_writes=2 cr4_reads=3 cr4_writes=2 all_returns_flags_clean=1 recovery_hazards=0
```

All 1,981 bytes decode, all 485 instructions are reachable, and every direct
branch targets an instruction boundary. A separate CFG dataflow audit proved:

- Every RET has a POPFQ-derived clean-flags state on all incoming paths.
- No RET is reachable while an owned-CR3 or PGE-clear recovery obligation is
  outstanding.
- CR4 restoration after an owned-root transition is not reachable before
  firmware-CR3 restoration.

The emitted binary is an ignored host artifact:

```text
artifacts/m01-013/audit/bin/Debug/net10.0/m01-013-transition.bin
  bytes=1981
  sha256=F1D94771DE8C8436CCDA1358A15F51DDE683AC3C77CBB0DC4A85B084888CA484
```

It was decoded as data and was never invoked.

### Complete host harness

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

The sole authoritative final run exited `0` after `357242` milliseconds.
Captured ignored evidence:

```text
artifacts/m01-013-evidence/host-final.stdout.log
  bytes=785608
  lines=6169
  sha256=C95857B877711EA7F4D8ECBAFF9A373E66E26B81DA1ABD2C335712FE2B01E3D7

artifacts/m01-013-evidence/host-final.stderr.log
  bytes=0
  sha256=E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855

artifacts/m01-013-evidence/host-final.result.json
  bytes=559
  sha256=5434513BDBA080324E6A92E70DBDAC30678694E2EDE3860D8A41F1276E9E8BED
```

Marker counts and positions:

```text
TEST_START m01.013                         count=1 line=6103
TEST_START m01.013.01.load                 count=1 line=6104
TEST_PASS  m01.013.01.load                 count=1 line=6105
TEST_START m01.013.02.preflight            count=1 line=6106
TEST_PASS  m01.013.02.preflight            count=1 line=6107
TEST_START m01.013.03.transition           count=1 line=6108
TEST_START m01.013.03.transition.01-.11    count=11 lines=6109-6129 odd
TEST_PASS  m01.013.03.transition.01-.11    count=11 lines=6110-6130 even
TEST_PASS  m01.013.03.transition           count=1 line=6131
CLROS_M01_013_READY                        count=1 line=6132
TEST_PASS  m01.013                         count=1 line=6133
TEST_FAIL <any>                            count=0
TEST_PASS host.tests                       count=1 line=6169
```

The final nonblank line is:

```text
TEST_PASS host.tests
```

### Naming, source firewall, and QEMU dry run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\check-naming.ps1

powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-qemu-smoke.ps1 -DryRun
```

Results:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
TEST_PASS workload.profile.tooling default single
TEST_PASS qemu.runner.dryrun
```

The QEMU command is only the current `SingleMinimalApi` dry-run construction.
It is not QEMU, OVMF, EFI, guest, paging, or CR3 evidence.

### Repository checks

```powershell
git diff --check
git status --short
```

`git diff --check` exited `0`; Git emitted only configured LF-to-CRLF
working-copy notices. Before commit, `git status --short` contained exactly:

```text
 M docs/architecture.md
 M docs/boot.md
?? done/step860.md
?? src/OS.Kernel/Memory/X64Cr3LoadStubPlan.cs
?? src/OS.Kernel/Memory/X64PagingModePreflightStubPlan.cs
?? src/OS.Kernel/Memory/X64ReversibleCr3TransitionStubPlan.cs
?? tests/OS.HostTests/X64Cr3LoadStubPlanTests.cs
?? tests/OS.HostTests/X64PagingModePreflightStubPlanTests.cs
?? tests/OS.HostTests/X64ReversibleCr3TransitionStubPlanTests.cs
```

No generated artifact is tracked.

## Evidence

Final candidate hashes before commit:

```text
7C736A0DB9A775337AEA8445F0E158A9FA84357AA8D3AC723141E8630FC3EF33  src/OS.Kernel/Memory/X64Cr3LoadStubPlan.cs
B04FD34108035FF0002104858279DD88F7D5E522EFAC99CDAC126C83D3DE9381  src/OS.Kernel/Memory/X64PagingModePreflightStubPlan.cs
08F10D935252A50E0DC1CCCDC472EFD3BF8FB3AEB8E88D6BDAC6426E5491A0D3  src/OS.Kernel/Memory/X64ReversibleCr3TransitionStubPlan.cs
D64FD56F0AB49C17ACCE2FE9D4810393064318B56A4B4125D2B86DDA8E4D25C9  tests/OS.HostTests/X64Cr3LoadStubPlanTests.cs
E1091AA63A145AA547B0ED00783DD572CA0F9B91FB58E30C67412B194564D046  tests/OS.HostTests/X64PagingModePreflightStubPlanTests.cs
A8D658BB5439D6B81D49BB4665657B47EAD181E3222C8C2D4FD6F6EF31512D21  tests/OS.HostTests/X64ReversibleCr3TransitionStubPlanTests.cs
307DE3FF1984E1A9A71E67804CD60BA85B0BC0A83D39DB8E74C403F60896C8E0  docs/boot.md
8CF38DF3B61A0B4DB83D93C3850CA06DF36F59590D5DA7E8606B4B7603DAA363  docs/architecture.md
```

Reference use was owner-authorized architectural and test influence. No
reference project, generator, dependency, or live runtime path was imported.
The supplied SharpOS snapshot has no recoverable Git revision, so exact local
paths and content hashes identify the studied files:

```text
088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47  references/SharpOS-main/OS/src/Kernel/Paging/Cr3Accessor.cs
B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500  references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs
B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6  references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs
```

`Cr3Accessor.cs` was used only to cross-check minimal CR3 opcodes and raw-value
preservation. `BootAsmGenerator.cs` influenced the exact bytes-versus-semantics
audit pattern. `PagingValidation.cs` supplied missing mapping, alignment, and
readback negative categories. The CLROS byte encoders, recovery graph, PODs,
decoder, CFG audit, and tests were independently authored.

Three parallel work streams implemented the composite, preflight, and
transition leaves. A separate final read-only adversarial pass rechecked
modeled address binding, storage geometry, same-root rejection, every return's
RFLAGS restoration, CR3/CR4 ordering, failure precedence, hostile POD
predicates, byte identity, control flow, register use, and no-stdlib closure.
All actionable findings were corrected before the authoritative run.

## Failures and corrections

- The first preflight success predicate did not rederive exact buffer capacity,
  entry offset/address alignment, recovered buffer base, overflow, and
  containment. Hostile copied success PODs could retain `Succeeded=true`.
  These numeric invariants and negative mutations now fail closed.
- Early transition revisions captured RFLAGS only after flag-changing
  validation. Entry PUSHFQ/POP R8 now occurs before the first TEST/CMP.
- The null-state and pre-CLI failure paths initially returned with flags left
  by TEST/CMP. Every one now restores the entry capture before RET, and a CFG
  test covers all 51 returns.
- An early final epilogue loaded RFLAGS through EAX and clobbered the return
  status. The status is retained in R9D, flags are restored, and only a
  flag-neutral MOV sets EAX afterward.
- The post-restore canary check initially overwrote an earlier operational
  failure. It now replaces only an otherwise-successful status, while
  CR3/CR4/RFLAGS/RSP restoration failures still take precedence.
- The transition initially allowed the owned root to equal the base of the
  captured firmware CR3 when only PWT/PCD differed. Live bytes, evaluator, and
  success POD now reject that no-op transition.
- Runtime state initially allowed a canary inside its own state record or
  active stack and did not reject all noncanonical/overflowed spans. The live
  preflight now validates low-canonical state/canary/stack intervals,
  state-before-canary placement, pairwise disjointness, nonzero canaries, and
  overflow before its first state write.
- The first evidence result derived its modeled address from the temporary host
  observation pointer. `Evaluate` now takes an explicit modeled state address,
  the 216-byte evidence POD carries it, and the composite binds it exactly to
  `StorageLayout.StateAddress`.
- The initial composite checked probe code/data disjointness only against one
  another. It now rejects all four probe-versus-stack-switch allocation
  collisions and independently revalidates them in copied plans/results.
- The first post-CLI check compared the entire live flags value with entry
  RFLAGS minus IF. Pre-CLI TEST/CMP legitimately changes arithmetic flags, so a
  real execution would usually fail before CR3. The final stream requires only
  IF clear and architectural bit 1 set at that checkpoint; the successful
  fixture deliberately changes CF. Exact full RFLAGS equality remains required
  after final restoration.
- Independent test review corrected one CR4 mutation vector that cleared an
  already-clear bit and a decoder group-opcode classification that could
  overreport register use.
- A preliminary redundant full-harness process was stopped after its owner was
  identified because composite integration was still moving. Its partial
  output is not evidence. The final 357242-millisecond run was launched once
  after source, tests, bytes, hashes, docs, and reviews were frozen.

## Deferred work

- The sources are not linked into or invoked by the canonical EFI project.
- No firmware page is allocated, reserved, owned, zeroed, populated with page
  tables, mapped, or protected by this step.
- LoaderCode/LoaderData provenance, identity reachability, and firmware-root
  reachability remain caller-attested plan evidence.
- No live CR0, CR3, CR4, IA32_EFER, CPUID, or RFLAGS value is read by CLROS.
- No live `CLI`, PGE cycle, CR3 load, TLB invalidation, page-zero check,
  RW-to-RX transition, page fault, EFI invocation, QEMU boot, or guest marker
  occurs.
- The composite validates intended RX and RW+NX mappings but does not enforce
  NX/W^X in hardware.
- PCID and five-level paging fail closed and remain unsupported.
- Maskable interrupts are excluded only in the modeled transition interval.
  NMI/SMI safety and controlled fault recovery remain residual risks until M02.
- A nonreturning fault while owned CR3 is active can only be classified later
  as timeout, reset, or triple fault by the live runner; host evidence cannot
  execute that path.
- `M01_PRE_CR3` and `M01_POST_CR3` are transcript values only. No guest emits
  them in this step.
- M01-019 owns coordinated table population, protection, state preparation, and
  reversible invocation. M01-020 owns explicit EFI source linking, M01-025
  owns boot-sequence integration, and M01-026 owns cumulative real-QEMU proof.
- No completion gate, CloudReady claim, hosted-.NET claim, Kestrel claim,
  provider claim, or production-readiness claim changes.

## Evidence boundary

This step proves only that the committed C# plans emit the exact documented
preflight and transition byte streams as ordinary data; structurally validate
the documented architectural prerequisites, raw-register restoration,
low-canonical storage, allocation/mapping/stack ownership evidence, one-shot
policy, callback-free event transcript, canary, marker order, and terminal
classification for the finite host corpus; reject the covered malformed,
overflow, alias, overlap, stale, rebound, default, timeout, reset, triple-fault,
and hostile-POD cases; preserve recovery obligations in independently decoded
control flow; compile under the exact no-stdlib/zero-reference boundary; and
remain absent from the canonical EFI artifact.

`CLROS_M01_013_READY` is a host-harness marker. It is not EFI, QEMU, OVMF,
guest, live-table, page-allocation, processor-state, interrupt, fault, NX/W^X,
CR3, TLB, page-zero, or completion-gate evidence. Every such live boundary
remains unproven.

