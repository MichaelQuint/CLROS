# Step 861 - Host-Audited Page Protection and INVLPG Plans

## Summary

Implemented Gitea issue `M01-014` as four no-stdlib-compatible,
host-audited contracts for a future one-page x64 protection transition:

1. `X64PageMap` transactionally maps, unmaps, or reprotects one existing 4 KiB
   leaf in a caller-owned flat image of a four-level page-table hierarchy.
2. `X64TlbInvalidationPlan` validates and materializes bounded, page-aligned,
   target-only invalidation records.
3. `X64InvlpgStubPlan` materializes and reads back the exact four-byte
   `invlpg [rcx]; ret` stream and validates an RX-only, one-shot invocation
   plan for the exact mutation target.
4. `X64ProtectionProbeStubPlan` materializes and reads back a separate
   six-byte constant-return stream, then audits a complete modeled
   RW+NX-to-RX transition, leaf readback, invalidation plan, return value,
   canary, caller-supplied allocation-record coherence, and event transcript.

All four boundaries are explicit ordinary-data models. They do not mutate a
processor-owned hierarchy, invoke either byte stream, issue `invlpg`, reload
CR3, or claim guest evidence. The implementation remains absent from the
canonical EFI compile closure.

## Why

M01-011 froze page-entry encoding and M01-012/M01-013 established fresh-table
and reversible-CR3 planning. M01-014 needed the next independently reviewable
leaf: prove that one existing 4 KiB leaf can move from writable/non-executable
to read/execute without an intermediate writable/executable state, while
binding the required target-only invalidation and a harmless post-transition
probe to the same page.

Keeping table mutation, invalidation materialization, x64 byte identity, and
aggregate transcript auditing separate makes malformed/default/overflowing
evidence fail before the sole leaf write. It also keeps the live boundary
owned by later serialized integration issues rather than allowing a host
boolean or modeled return value to masquerade as CPU or QEMU proof.

## Files changed

- `src/OS.Kernel/Memory/X64PageMap.cs`
  - Adds append-only operation/status PODs and a bounded unsafe
    pointer/count/capacity core over caller-owned table-image records, raw
    entries, and essential ranges.
  - Validates every declared span, alignment, capacity, overlap, page-image
    record, hierarchy edge, request, essential range, replacement entry, and
    invalidation range before the sole 64-bit leaf store.
  - Retains the validated page-image set's typed first-to-last physical
    envelope in each success result so later aggregate policy can bind the
    mutated hierarchy to its declared allocation.
  - Supports one 4 KiB `Map`, `Unmap`, or protection-only `Protect`; it
    allocates no intermediate table and splits no large page.
  - Rejects page zero, W+X, malformed or missing tables, out-of-image child
    pointers, 1 GiB/2 MiB collisions, duplicate map, double unmap, unchanged
    protection, and every declared essential target.
- `src/OS.Kernel/Memory/X64TlbInvalidationPlan.cs`
  - Adds bounded invalidation-range, record, and result PODs.
  - Materializes at most 4,096 exact 4 KiB target records in ordinal order,
    after complete destination preflight.
  - Explicitly reports that it performs no live invalidation and reloads no
    CR3.
- `src/OS.Kernel/Memory/X64InvlpgStubPlan.cs`
  - Adds exact C# byte materialization/readback for `0F 01 39 C3`.
  - Requires a 16-byte-aligned entry wholly inside one modeled 4 KiB
    page; the aggregate separately binds that page to a caller-supplied
    one-page `LoaderCode` allocation.
  - Produces an invocation plan only for one verified target record and an
    RX/non-writable code page; host tests never invoke the bytes.
- `src/OS.Kernel/Memory/X64ProtectionProbeStubPlan.cs`
  - Adds exact C# byte materialization/readback for
    `B8 4B 4F 53 4F C3`.
  - Requires caller-supplied records for its own identity-mapped one-page
    `LoaderCode` allocation and exact `PageProtectionProbe` reservation.
  - Audits disjoint physical and virtual probe-code, transition-code, and
    table/state allocations; physical containment of the complete page-image
    envelope inside the caller-attested table/state allocation; exact
    RW+NX-to-RX leaf preservation; readback; target/ordinal/entry-coherent
    INVLPG evidence; one modeled invocation; expected return; unchanged
    nonzero canary; and nine ordered events.
  - Requires the caller-supplied table/state policy value to remain exactly
    RW+NX; this is not observed hardware protection.
- `tests/OS.HostTests/X64PageMapTests.cs`
  - Adds the sole `M01-014` aggregate coordinator and page-map/invalidation
    suites.
  - Freezes exact append-only enum names/values and unmanaged,
    reference-free POD layouts.
  - Covers positive map/unmap/remap/protect behavior, exactly-one-leaf writes,
    complete negative snapshots, hostile pointer/span/alias/capacity cases,
    page-zero/W+X/essential-range policy, malformed walks, retained physical
    envelopes, and a zero-assembly-reference no-stdlib compile closure.
- `tests/OS.HostTests/X64InvlpgStubPlanTests.cs`
  - Covers exact byte/hash identity, materialization/readback atomicity,
    allocation and entry geometry, one-shot RX-only invocation binding,
    target/ordinal mismatch, W+X/RW+NX rejection, and explicit non-execution.
- `tests/OS.HostTests/X64ProtectionProbeStubPlanTests.cs`
  - Covers exact enum/POD/default contracts, byte/hash identity,
    materialization/readback, separate allocation and reservation ownership,
    physical and virtual overlap, out-of-allocation binding, table-state
    protection and page-image containment, unrelated/truncated state arenas,
    mutation/readback/invalidation coherence, invocation/return/canary
    mismatch, all transcript failures, and source non-execution.
- `docs/boot.md`
  - Records the exact M01-014 mutation, storage, byte, allocation, event,
    provenance, marker, test, and evidence-boundary contracts.
- `docs/architecture.md`
  - Records the host-tested capability and its live paging, EFI, SMP,
    concurrency, and effective-permission limitations without changing a
    completion gate.
- `done/step861.md`
  - This evidence record.

No EFI entry, EFI project, boot sequence, diagnostic probe, README,
completion-gate, backlog manifest, synchronization script, or reference file
changed.

## Public contract

The exact public POD sizes are:

```text
X64PageTableImagePage                    24
X64EssentialMappingRange                32
X64PageMapRequest                       64
X64PageMapResult                       264

X64TlbInvalidationRange                 40
X64TlbInvalidationRecord                24
X64TlbInvalidationPlanResult            88

X64InvlpgStubPlanResult                104
X64InvlpgInvocationRequest              56
X64InvlpgInvocationPlanResult           88

X64ProtectionProbeStubPlanResult        80
X64ProtectionProbeEvent                 24
X64ProtectionProbeObservation           56
X64ProtectionProbePlanResult           744
```

Every new enum is byte-backed, append-only, and exactly name/value frozen by
tests; zero is never success. Every public record above is unmanaged and
reference-free. `TableImageOutsideStateAllocation = 59` is the final
protection-audit status in this slice.

The page-map bounds are:

```text
PageSize                         4096
MaximumPageImages                  64
MaximumEntryCount               32768
MaximumEssentialRangeCount        128
MaximumEssentialRangePages       4096
MaximumInvalidationPageCount     4096
```

The exact INVLPG stream identity is:

```text
Bytes                    0F 01 39 C3
RequiredByteLength       4
AuditedInstructionCount  2
EntryAlignment           16
SHA-256                  FEF29CD9A7B4B2A9C53BC69C7893BC37FFFACECF2093CD943C5F43139C2CF53F
```

It consumes the Windows-x64 first integer argument in RCX, writes no
nonvolatile GPR or XMM6-XMM15 register, has no relocation, and returns
directly. The plan constants freeze
`PerformsLiveInvalidation=false`, `ReloadsCr3=false`,
`InvokesStub=false`, and `HostTestsInvokeBytes=false`.

The exact protection-probe stream identity is:

```text
Bytes                    B8 4B 4F 53 4F C3
RequiredByteLength       6
AuditedInstructionCount  2
EntryAlignment           16
ExpectedReturnValue      0x4F534F4B
RequiredEventCount       9
SHA-256                  BBA15FCF0EEE9F4112C1ABB203EB833D44A09E9BCEDB29456ACAE9B466EBA374
```

Its exact successful transcript is:

```text
StubMaterialized
StubReadBackVerified
InitialReadWriteNoExecuteVerified
ProtectionChangedReadExecute
LeafReadBackVerified
TargetInvalidationPlanned
InvlpgInvocationPlanned
ProbeInvocationPlanned
ConstantReturnAndCanaryObserved
```

The aggregate accepts the modeled observation only with
`IsLiveEvidence=false`. It contains no field or conversion that can promote
that observation to live evidence.

## Tests run

All commands ran from `C:\Projects\CLROS-M01-014` on 2026-07-26 with .NET SDK
`10.0.302`, branch `codex/M01-014-rw-rx-protection-probe`, and base commit
`3ad97a444bcf30ef6b306106e72d6ba313d62a53`.

### Production, host-test, and complete solution builds

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\CLROS.slnx --nologo -m:1
```

Both final builds exited `0` with `0` warnings and `0` errors.

### Complete host harness

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

The final complete harness exited `0` and emitted:

```text
TEST_START m01.014
TEST_START m01.014.01.map
TEST_PASS m01.014.01.map
TEST_START m01.014.02.invlpg
TEST_PASS m01.014.02.invlpg
TEST_START m01.014.03.protection
TEST_PASS m01.014.03.protection
CLROS_M01_014_READY
TEST_PASS m01.014
TEST_PASS host.tests
```

No `TEST_FAIL` marker was emitted. The aggregate coordinator suppresses
`CLROS_M01_014_READY` and its pass marker whenever a child reports failure.

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

The QEMU command is construction-only. It is not QEMU, OVMF, EFI, guest,
page-table, protection, INVLPG, CR3, or TLB evidence.

### Repository checks

```powershell
git diff --check
git status --short
```

`git diff --check` exited `0`; Git emitted only configured LF-to-CRLF
working-copy notices. Before commit, the intentional status is:

```text
 M docs/architecture.md
 M docs/boot.md
?? done/step861.md
?? src/OS.Kernel/Memory/X64InvlpgStubPlan.cs
?? src/OS.Kernel/Memory/X64PageMap.cs
?? src/OS.Kernel/Memory/X64ProtectionProbeStubPlan.cs
?? src/OS.Kernel/Memory/X64TlbInvalidationPlan.cs
?? tests/OS.HostTests/X64InvlpgStubPlanTests.cs
?? tests/OS.HostTests/X64PageMapTests.cs
?? tests/OS.HostTests/X64ProtectionProbeStubPlanTests.cs
```

No generated artifact is tracked.

## Evidence

Final candidate hashes before commit:

```text
E6F3EAEF5BDC7B8C42A019AA585F856642D43FB7B35EB8B6874BE54530238DC9  src/OS.Kernel/Memory/X64PageMap.cs
532CD4AEAC02403C283DAFBFEBAD68458E709A541781D75251BAA5859CFF77D6  src/OS.Kernel/Memory/X64TlbInvalidationPlan.cs
1771EFEE34BB453087F778A20066D20D2E82DB960127BC409D5797571CC449D4  src/OS.Kernel/Memory/X64InvlpgStubPlan.cs
0CF43A8787B6A47B637BD17DA5CA6E44026B323DBD20F0E3327CEC8A6909B5F5  src/OS.Kernel/Memory/X64ProtectionProbeStubPlan.cs
5B9CC2554945DEB9A3D78B720A87E995CAC6F13EBA7E36D088E101635C379DD8  tests/OS.HostTests/X64PageMapTests.cs
48DE5B8F29717F65D71E9279A3B440FDB8C5E9DFB28C65DB4545A7F73E646E38  tests/OS.HostTests/X64InvlpgStubPlanTests.cs
59F2E176DF1EFF429A1082BE586373830710D5E55D47A5CCD6EC4668B178E6E3  tests/OS.HostTests/X64ProtectionProbeStubPlanTests.cs
308D5A5A89AB8FE324D5C15D79086C37800817DECB9281593436B3E8205DCF02  docs/boot.md
C1E23C09DF8EF655BAA4A03B910599A7E797C80310BE013355A06240E2A7D73C  docs/architecture.md
```

Reference use was owner-authorized architectural and test influence. No
reference project, dependency, generator, or live runtime path was imported.
The supplied SharpOS snapshot has no recoverable Git revision, so exact local
paths and content hashes identify the studied files:

```text
35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC  references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs
DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652  references/SharpOS-main/OS/src/Kernel/Paging/PageFlags.cs
B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6  references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs
```

`X64PageTable.cs` supplied a four-level indexing/walk cross-check and
large-page collision category, but CLROS rejects its global state, blind
physical-pointer casts, partial mutations, permissive protection behavior,
and CR3-wide flush. `PageFlags.cs` was a protection-bit cross-check;
M01-014 instead consumes M01-011's strict typed protection/cache contracts.
`PagingValidation.cs` supplied test-only negative categories that were
independently recreated against the CLROS contracts.

The canonical upstream is `https://github.com/daniilvaino/SharpOS`.
Owner-supplied permission authorizes reuse of SharpOS-authored source, and the
upstream independently corroborates CC0-1.0 at evidence commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`. No file-local third-party term is
overridden.

Parallel test and review work independently audited INVLPG identity, aggregate
protection evidence, pointer preflight, failure atomicity, enum/POD stability,
allocation binding, and no-stdlib closure. All actionable findings were
corrected before the final complete harness.

## Failures and corrections

The first complete harness attempt preserved this exact failing aggregate:

```text
M01_014_MAP_CASE_FAIL source_and_nostdlib_closure_remain_isolated
M01_014_PROTECTION_CASE_FAIL append_only_enums_pods_defaults_and_nonlive_flags
M01_014_PROTECTION_CASE_FAIL leaf_return_canary_count_and_entry_mismatches_fail InvalidOperationException: Virtual fixture invalid: MisalignedAddress
M01_014_PROTECTION_CASE_FAIL source_remains_plan_only_without_live_execution
TEST_FAIL m01.014 failures=4
HOST_TEST_EXIT_CODE=-532462766
```

- The first complete harness exposed a source-firewall false positive because
  an XML comment contained the scanner token `delegate `. The comment was
  rewritten without weakening the scanner.
- The first protection contract test exposed a stale declared aggregate size
  (`840`) after the result layout was tightened. An isolated runtime-size
  diagnostic proved that intermediate unmanaged layout was `728`. The later
  page-image-envelope fields increased the final aggregate layout to `744`;
  declarations and exact tests now agree.
- One malformed observation test accidentally used the aligned-address fixture
  to construct its deliberately misaligned entry and threw before reaching
  the status path. It now uses the explicit unaligned constructor and asserts
  `InvocationEntryMismatch`.
- The source-boundary test originally searched its own source for the literal
  `delegate*`, thereby matching the assertion string itself. The token is now
  assembled from two literals so the test scans only actual use.
- Adversarial review found missing exact enum-name freezing, explicit
  reference-free POD checks, an out-of-allocation probe-stub case, incomplete
  table snapshots for some walk failures, weak allocation domain overlap, a
  missing table-state-protection check, and incomplete target/ordinal/entry
  coherence. Each finding gained a fail-closed production check or direct
  negative test before the final run.
- The final independent audit then proved that the aggregate accepted an
  otherwise-valid but unrelated table/state allocation because
  `X64PageMapResult` did not retain the validated page-image physical
  envelope. The result now retains that typed envelope; the aggregate requires
  its complete containment in the caller-attested RW+NX `LoaderData` record;
  and exact unrelated and truncated-arena tests fail with
  `TableImageOutsideStateAllocation`.
- The same audit identified an execute-before-protect classification edge at
  zero-based event index 3. The specific
  `ExecuteBeforeProtectRejected` classification now includes that boundary
  and has a direct regression test.
- Duplicate exploratory full-harness runs were stopped during review; their
  child processes were explicitly terminated. They are not evidence. The
  final uninterrupted harness above is authoritative.
- One post-envelope `--no-build` attempt was stopped immediately after its
  host-test assembly was found to predate the final aggregate-size constant.
  PID 19688 was terminated, the canonical host-test project rebuilt with zero
  warnings/errors, and only the later uninterrupted run is evidence.

## Deferred work and evidence boundary

This issue proves only host mutation of caller-owned page-table-image bytes,
bounded invalidation-record materialization, and audited identity/coherence of
future INVLPG and constant-return protection-probe bytes and transcripts.

It does **not** prove that supplied unsafe pointers are mapped, stable, or
exclusively serialized; allocate/zero/populate live table pages; perform an
aligned store against processor-owned tables; inspect inherited effective
permissions; split a large page; enforce concurrent or SMP ordering; execute
either byte stream; issue `invlpg`; reload CR3; perform a TLB shootdown; prove
page zero absent in hardware; link the source into `src/OS`; run under
QEMU/OVMF; or advance a completion gate.

M01-019 owns coordinated live table preparation and invocation, M01-020 owns
explicit EFI source linking, M01-025 owns default boot wiring, and M01-026
owns cumulative real-QEMU evidence. Later platform work owns SMP shootdown,
PCID/global-page policy, and memory-ordering integration.
