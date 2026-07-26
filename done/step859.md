# Step 859 - Required Boot Identity-Mapping Plan

## Summary

Implemented Gitea issue `M01-012` as the canonical caller-owned value planner
for the low-canonical identity mappings required by a future CLROS-owned x64
page-table hierarchy.

The new contract:

- Requires explicit mappings for the executing image, owned stack, boot
  handoff, owned serial stub, ACPI scratch, and framebuffer metadata.
- Requires exactly one boot-stack guard, validates it as an exclusion, and
  never emits it as a mapping.
- Permits additional explicitly required owned mappings without allowing them
  to satisfy one of the six mandatory semantic roles.
- Accepts only aligned, nonempty, typed physical half-open ranges with exact
  page and byte geometry.
- Rejects page zero, addresses outside the caller-attested 36-52-bit physical
  width, and ranges beyond the exclusive low-canonical identity boundary.
- Accepts only supervisor R, RW, or RX leaves and rejects every
  writable-executable combination.
- Reuses the M01-011 WB/UC/WC, PAT, NXE, WP, four-level-paging, physical-width,
  and 4 KiB PTE encoding contract instead of defining a second policy surface.
- Validates raw IA32_PAT index zero as WB for paging-structure walks,
  independently of the selected leaf-WB index.
- Deterministically normalizes compatible overlapping and adjacent ranges
  without permission or cache-policy promotion.
- Preserves contributing-purpose provenance for each normalized union while
  keeping diagnostic source indexes relative to caller order.
- Calculates the exact fresh PML4/PDPT/PD/PT page requirement for 4 KiB leaves
  without iterating over every mapped page.
- Performs complete multi-pass preflight and a separate reconstruction check
  before one write-only emission pass.
- Preserves the full destination span on every returned failure.
- Exposes only unmanaged POD values and an unsafe pointer/count/capacity core
  that compiles with `NOSTDLIB_EFI` and zero assembly references.

The implementation is metadata planning and host evidence only. It does not
allocate, zero, populate, install, or activate any page table.

## Why

M01-013 and later paging work need one exact statement of which boot resources
must remain reachable while CLROS moves from firmware mappings toward its own
address space. Constructing tables before freezing that statement would mix
range discovery, ownership, permission policy, cache policy, table capacity,
table allocation, live writes, and CR3 activation into one unsafe boundary.

This step deliberately separates:

- Caller-attested boot-resource identity from future live discovery.
- A required mapping from a guard exclusion.
- Normalized mapping intent from future table-page ownership and addresses.
- Leaf protection/cache policy from paging-structure page-walk caching.
- Fresh-hierarchy capacity accounting from future table construction or reuse.
- Deterministic normalized output from caller-order-relative diagnostic
  indexes.
- Host-test readiness from EFI linkage, QEMU evidence, and a completion gate.

## Files changed

- `src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs`
  - Adds the append-only purpose and status contracts.
  - Adds the 40-byte requirement, 64-byte normalized range, and 176-byte
    result PODs.
  - Adds the bounded, allocation-free planning core, policy checks,
    deterministic normalization, exact hierarchy accounting, capacity gates,
    and failure-atomic emission.
- `tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs`
  - Adds fourteen self-registering layout, semantic, negative, overflow,
    capacity, hostile-POD, pointer, no-stdlib, evaluated-EFI-closure, and marker
    groups.
  - Includes regression coverage for empty destination spans, partial
    nonempty aliasing, unordered transitive merging, complete capacity-failure
    canaries, and every frozen reserved byte.
- `docs/boot.md`
  - Records the exact public contract, normalization and accounting rules,
    pointer/stability requirements, tests, provenance, limitations, and
    evidence boundary.
- `docs/architecture.md`
  - Records the host-tested capability, unsupported behavior, tests,
    limitations, and later integration owners without changing a gate.
- `done/step859.md`
  - This evidence record.

No EFI entry, EFI project, boot sequence, diagnostics probe, README,
completion-gate, backlog manifest, sync script, or reference file changed.

## Public contract

`X64BootIdentityMappingPurpose : byte` is append-only:

```text
0 Uninitialized
1 ExecutingImage
2 OwnedStack
3 BootHandoff
4 SerialStub
5 AcpiScratch
6 FramebufferMetadata
7 BootStackGuard
8 AdditionalRequired
```

The required emitted-purpose mask is `0x7e`, covering values 1 through 6.
`BootStackGuard` is required exactly once but never emitted.
`AdditionalRequired` is optional, repeatable, and emitted.

The exact public limits and declarations are:

```text
X64BootIdentityMappingRequirement.StructureSize = 40
X64BootIdentityMappingRange.StructureSize       = 64
X64BootIdentityMappingPlanResult.StructureSize  = 176
PageSize                                        = 4096
MaximumSourceCount                              = 256
MaximumDestinationCount                         = 512
RequiredPointerAlignment                         = 8
LowCanonicalEndExclusive                         = 0x0000800000000000
RequiredPurposeMask                              = 0x7e
X64BootIdentityMappingPlanResult.NoIndex         = ~0UL
UsesOnly4KiBLeaves                               = true
PerformsLiveMapping                              = false
AllocatesPageTables                              = false
PopulatesPageTables                              = false
```

The sole production entry point is:

```text
X64BootIdentityMappingPlan.Create(
    X64BootIdentityMappingRequirement* source,
    ulong sourceCount,
    ulong sourceCapacity,
    X64BootIdentityMappingRange* destination,
    ulong destinationCapacity,
    ulong availableTablePageCapacity,
    X64PageTableEncodingPreconditions preconditions)
```

Before dereferencing caller storage, it validates compiler-observed POD sizes,
logical count/capacity relationships, caps, null pointers, 8-byte pointer
alignment, full compiler-sized span arithmetic, and nonempty declared-span
aliasing. A zero-capacity destination is an empty span and therefore cannot be
misclassified as a source alias. Callers must keep the complete declared spans
mapped, exclusively owned, and stable for the entire call.

Every mapped input uses exact 4 KiB geometry and remains strictly below
`LowCanonicalEndExclusive`. The endpoint may equal that exclusive boundary.
The selected physical width and M01-011 preconditions are revalidated before
range processing. Only R, RW, and RX plus WB, UC, or WC are accepted.

Compatible overlapping or adjacent ranges are transitively merged. An emitted
range's `ContributingPurposeMask` summarizes purposes that contributed any
bytes to that union; it does not claim that every page in the union serves
every recorded purpose. Incompatible overlap fails, while adjacent ranges
with different policies remain separate.

For the fresh four-level 4 KiB-leaf hierarchy:

```text
PML4 pages = 1
PDPT pages = distinct (virtualAddress >> 39) regions
PD pages   = distinct (virtualAddress >> 30) regions
PT pages   = distinct (virtualAddress >> 21) regions
total      = PML4 + PDPT + PD + PT
```

Capacity is checked before emission. Analysis evidence, including required
destination capacity and union/table accounting, remains available for
destination- or table-capacity failures. `OutputRangeCount` counts records
actually emitted and remains zero on every failure.

The one-byte append-only status contract is:

```text
 0 Uninitialized                       24 InvalidCachePolicy
 1 Success                             25 InvalidGuardPolicy
 2 InvalidSourceCount                  26 MultipleGuards
 3 TooManySources                      27 MissingGuard
 4 SourceCapacityTooSmall              28 GuardOverlap
 5 CapacityExceedsLimit                29 MissingRequiredPurpose
 6 MissingSource                       30 PageZeroMappingRejected
 7 MissingDestination                  31 NonCanonicalIdentityRange
 8 MisalignedSource                    32 PhysicalAddressOutsideWidth
 9 MisalignedDestination               33 IncompatibleProtectionOverlap
10 SourceSpanOverflow                  34 IncompatibleCacheOverlap
11 DestinationSpanOverflow             35 DestinationCapacityTooSmall
12 SourceDestinationOverlap             36 TooManyOutputRanges
13 ContractLayoutMismatch               37 UnionPageCountOverflow
14 InvalidRequirementState              38 UnionByteCountOverflow
15 InvalidPurpose                       39 TablePageCountOverflow
16 InvalidPhysicalStart                 40 TableByteCountOverflow
17 InvalidPhysicalEnd                   41 TableCapacityTooSmall
18 EmptyOrReversedRange                 42 InvalidPreconditions
19 MisalignedRange                      43 PageWalkCacheNotWriteBack
20 GeometryOverflow                     44 LeafEncodingRejected
21 GeometryMismatch                     45 OutputConstructionFailure
22 InvalidProtection                    46 AccountingMismatch
23 WritableExecutableRejected
```

## Tests run

All final commands ran from `C:\Projects\CLROS-M01-012` on 2026-07-26 with
.NET SDK `10.0.302`, branch
`codex/M01-012-identity-mapping-plan`, and base commit
`6774df31a4f8dade449356e2be553383ac377d45`.

### Production, host-test, and complete solution builds

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\CLROS.slnx --nologo -m:1
```

Every command exited `0` with `0` warnings and `0` errors after the final
implementation and review corrections.

### Focused M01-012 suite

```powershell
dotnet run --project `
  .\artifacts\m01-012-focused\m01-012-focused.csproj --no-restore
```

The ignored focused runner exited `0`. Its final log is:

```text
artifacts/m01-012-tests/focused-regressions.stdout.log
  bytes=3990
  sha256=E718C1C5B0B4D8D966444F50E259221549E2952D09AB3D8CD6BED6C664A43857
```

It contains all fourteen numbered pass markers, one
`CLROS_M01_012_READY`, one aggregate `TEST_PASS m01.012`, and its own
`TEST_PASS m01.012.focused.runner`.

### Complete host harness

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Authoritative post-review result: exit `0`. The test child reached its terminal
output after `277252` milliseconds. The evidence wrapper returned after
`604392` milliseconds because Windows `Start-Process -Wait` also retained an
idle reusable MSBuild node; `dotnet build-server shutdown` stopped that server
cleanly after the child had exited and allowed the wrapper to preserve the
actual exit code.

Captured ignored evidence:

```text
artifacts/m01-012-evidence/host-20260726T113007090Z.stdout.log
  bytes=391753
  lines=6138
  sha256=671E49679BBFD63FE88BDCB2F35FB53C4FAE1ABCB401940D17BF8A1C183B8CBB

artifacts/m01-012-evidence/host-20260726T113007090Z.stderr.log
  bytes=0
  sha256=E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855

artifacts/m01-012-evidence/host-20260726T113007090Z.result.json
  bytes=433
  sha256=ACC2D7203944CDD17580EBBC96F6008BDD29B15FC917A4C32E215EE0B2DF82BD
```

Marker counts:

```text
TEST_START m01.012                         count=1  line=6072
TEST_START m01.012 <numbered group>        count=14 lines=6073-6099 odd
TEST_PASS m01.012 <numbered group>         count=14 lines=6074-6100 even
CLROS_M01_012_READY                        count=1  line=6101
TEST_PASS m01.012                          count=1  line=6102
TEST_FAIL m01.012 <reason>                 count=0
TEST_FAIL <any>                            count=0
TEST_PASS host.tests                       count=1  line=6138
```

The complete log's last nonblank line is:

```text
TEST_PASS host.tests
```

### Test coverage

The fourteen groups cover:

1. Exact purpose/status values, every POD size/offset, defaults, reserved
   bytes, and explicit non-live constants.
2. Every individually missing required purpose plus missing, multiple, and
   malformed guards.
3. A realistic split RX/RW image, all required mappings, exact output,
   caller-order-relative diagnostics, and order-independent normalized output.
4. Duplicate, nested, partial-overlap, adjacent, and unordered transitive
   compatible merges with exact union accounting and contributor masks.
5. R/RW/RX and WB/UC/WC overlap conflicts with exact source indexes.
6. All 256 protection/cache bytes, W+X, malformed PAT state, unsupported
   cache selection, duplicate selected leaf-WB indexes, and independent raw
   PAT-index-zero page-walk WB.
7. Guard exclusion, guard overlap, and forged mapped page-zero rejection.
8. Alignment, empty/reversed ranges, geometry mismatch/overflow, 36/48/52-bit
   physical-width boundaries, and the exclusive low-canonical ceiling.
9. Exact table-page counts across 2 MiB, 1 GiB, and 512 GiB boundaries,
   sparse shared buckets, additional table-arena mappings, and exact/short
   table capacity.
10. Null/alignment/count/capacity/span overflow, empty destination, partial
    and full alias, maximum limits, destination canaries, and source
    immutability.
11. Hostile initialization/reserved/enum/precondition bytes, every reserved
    result/range byte, failure atomicity, and capacity-failure full-span
    canaries.
12. First-party source rules and an exact `NOSTDLIB_EFI`, no-framework,
    zero-assembly-reference compile closure.
13. Evaluated `SingleMinimalApi` and `MultiMinimalApi` EFI closures excluding
    both production and test sources.
14. Exact marker ownership, counts, order, failure-before-readiness behavior,
    and absence of guest-marker claims.

### Naming and source firewall

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\check-naming.ps1
```

Result: exit `0` with:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

### QEMU dry run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-qemu-smoke.ps1 -DryRun
```

Result: exit `0` with:

```text
TEST_PASS workload.profile.tooling default single
TEST_PASS qemu.runner.dryrun
```

This validates the current `SingleMinimalApi` runner/profile command
construction only. It is not QEMU, OVMF, EFI, guest, paging, or boot evidence.

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
?? done/step859.md
?? src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs
?? tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs
```

No generated artifact is tracked.

A final scoped audit reported:

```text
changed paths=5
unexpected changed paths=0
first-party .c/.cc/.cpp/.cxx/.h/.hh/.hpp/.asm/.s files=0
reference imports in non-reference .slnx/.csproj/.props/.targets=0
secret-pattern hits in the five candidate files=0
```

## Evidence

Final candidate hashes before commit:

```text
ECB90DF09462EE357DA6326FBAB0909D62C8AABD08A9FE60A59768892FFD57BA  src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs
FDBD7D76A419253676D85309C6FF8E9DF2D9B4E56D688764BD9561BFC2B586DE  tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs
9E74823104DD4D437B7AC70D9519AFBFA8CDAA421C5D4BE8056B0D89761F006D  docs/boot.md
1ED340E6FF4F8ED5F3B15D31E91B52AA403D2B0B6ECA8430940A0FC7901D2915  docs/architecture.md
```

Reference use was owner-authorized architectural and test influence. CLROS did
not import a reference project or treat reference behavior as completion
evidence. The supplied local SharpOS snapshot has no recoverable Git revision,
so its exact paths and content hashes are the preserved source identity:

```text
DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652  references/SharpOS-main/OS/src/Kernel/Paging/PageFlags.cs
35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC  references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs
B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6  references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs
```

The canonical upstream is `https://github.com/daniilvaino/SharpOS`.
Owner-supplied permission authorizes reuse of SharpOS-authored source, and
upstream independently corroborates CC0-1.0 at license-evidence commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`. That commit is license evidence,
not an asserted revision for the supplied snapshot. File-local third-party
terms remain separate.

`PageFlags.cs` was used to cross-check x64 flag semantics.
`X64PageTable.cs` influenced hierarchy/table-sharing analysis.
`PagingValidation.cs` supplied missing/duplicate/neighbor/conflict categories
for independently authored host vectors. CLROS does not adopt live table
cloning, reference physical allocation, direct table-pointer mutation,
permissive user/global leaves, large-page mutation, CR3/TLB behavior, or
protection loosening.

Three independent work streams implemented or reviewed production, tests, and
documentation. Two later read-only adversarial audits checked normalization,
transitive merging, capacity and failure atomicity, pointer spans, table
accounting, PAT policy, all public values/layouts, no-stdlib constraints, test
coverage, documentation accuracy, provenance, and live-evidence boundaries.
Every blocking and actionable finding was corrected before the authoritative
run.

## Failures and corrections

- The initial `LowCanonicalEndExclusive` literal inferred `System.Int64` in
  the exact zero-reference closure. Adding the explicit `UL` suffix corrected
  the production declaration; the final no-stdlib group passes.
- Independent review found that an aligned zero-capacity destination pointer
  inside the source address interval was falsely classified as an overlap.
  Empty destination spans are now excluded from alias detection, while
  nonempty partial/full overlap remains rejected.
- Independent review found regression gaps for unordered transitive merging,
  complete destination preservation on both capacity failures, and every
  reserved byte in the output/result PODs. Those vectors and full-byte
  canaries were added before the final run.
- Independent review narrowed order-independence claims to normalized output
  and accounting because diagnostic source indexes intentionally remain
  relative to caller order.
- Independent review corrected the documentation from a generic “two-pass”
  description to the actual multi-pass, two-phase preflight-then-emission
  algorithm and added all public structure-size and non-live constants.
- During parallel development, two preliminary full-harness runs overlapped.
  The redundant planner-owned run was identified by exact parent/child process
  audit and stopped; its partial logs are not evidence. A concurrent host
  build also encountered transient file locks from those duplicate runners.
  The final authoritative harness was run once with no competing copy.
- A preliminary pre-`UL` full run failed only the exact no-stdlib closure and
  is not retained as final evidence.
- The authoritative test child reached `TEST_PASS host.tests` and exited
  successfully, but Windows `Start-Process -Wait` continued waiting on its
  reusable idle MSBuild node. The complete process tree and terminal log were
  inspected, `dotnet build-server shutdown` stopped the build server cleanly,
  and the wrapper then preserved the child's real exit code `0`. The child
  output completion time and longer wrapper duration are reported separately.

## Deferred work

- The source remains outside both canonical EFI workload-profile compile
  closures and is not called by `EfiMain`.
- The caller still has to discover and attest correct image-section, stack,
  handoff, serial-stub, ACPI, framebuffer-metadata, guard, and table-arena
  ranges.
- The plan cannot prove caller storage is mapped or stable, that supplied
  ranges are owned, or that policies match live linker/firmware/device state.
- No table page is allocated, reserved, owned, zeroed, addressed, mapped,
  populated, or protected.
- No page-table arena is automatically inserted into the plan.
- No guard is made inaccessible in hardware.
- No current page table is walked or reused.
- No live CPUID, CR0, CR4, EFER, IA32_PAT, MTRR, or CR3 state is read or
  programmed.
- No live mapping, RW-to-RX transition, TLB invalidation, CR3 load, page fault,
  EFI execution, QEMU execution, or guest marker occurs.
- The plan intentionally uses only 4 KiB leaves; later implementations may
  separately justify large-page policy.
- Defensive arithmetic statuses that are unreachable under the current
  256-source and low-canonical caps remain frozen for fail-closed future
  changes.
- M01-013 owns opt-in CR3 loading over later constructed tables.
  M01-014 owns caller-owned map/unmap/protect/TLB planning.
  M01-019 owns the coordinated reversible paging phase.
  M01-020 owns explicit EFI linkage.
  M01-025 owns boot-sequence integration.
  M01-026 owns cumulative real-QEMU evidence.
- No completion gate, CloudReady claim, hosted-.NET claim, Kestrel claim,
  provider claim, or production-readiness claim changes.

## Evidence boundary

This step proves only that the committed no-stdlib-compatible CLROS planner
produces the documented deterministic normalized identity-mapping records and
fresh four-level 4 KiB table-page capacity for the finite host corpus; fails
closed for the covered missing roles, guard, page-zero, geometry, overflow,
physical-width, protection, cache, PAT, pointer, alias, capacity, malformed
POD, and hostile-input cases; preserves the complete destination on every
returned failure; and remains absent from both current canonical EFI
workload-profile closures.

`CLROS_M01_012_READY` is a host-harness marker. It is not EFI, QEMU, OVMF,
guest, live-table, page-allocation, guard-enforcement, processor-state,
NX/W^X, CR3, TLB, page-fault, or completion-gate evidence. All such live
boundaries remain unproven.
