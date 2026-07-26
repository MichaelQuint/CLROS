# Step 858 - X64 Page-Table Entry Value Contract

## Summary

Implemented Gitea issue `M01-011` as the first canonical CLROS value contract
for x64 PML4E, PDPTE, PDE, and PTE encoding and strict decoding.

The new contract:

- Defines stable one-byte `PageProtection` and `PageCachePolicy` values without
  a framework-dependent flags attribute.
- Accepts only supervisor `R`, `RW`, or `RX` leaf mappings and rejects `W`,
  execute-only, unknown protection bits, and every writable-executable
  combination.
- Represents an explicit unmapped request separately from a default,
  uninitialized request.
- Encodes 4 KiB PTE, 2 MiB PDE, and explicitly capability-gated 1 GiB PDPTE
  leaves, plus canonical nonleaf table pointers.
- Rejects zero, misaligned, above-width, or silently maskable physical
  addresses for caller-supplied 36-52-bit physical-address widths.
- Binds WB, UC, and WC leaf encodings to a validated caller-supplied IA32_PAT
  value and selected PAT indexes.
- Validates raw IA32_PAT index zero as WB for paging-structure walks,
  independently of the selected leaf WB index.
- Requires explicit caller-supplied NXE, CR0.WP, four-level-paging, physical
  width, 1 GiB-page capability, and PAT observations.
- Strictly decodes hardware Accessed/Dirty observations while rejecting
  unsupported user, global, software, protection-key, large-page-reserved, and
  above-width bits.
- Provides an explicit protection-only re-encoder that either clears or
  preserves Accessed/Dirty state according to a required policy.
- Provides a bounded, no-allocation, two-pass unsafe batch encoder that
  validates all requests and complete caller-declared spans before the first
  destination write.
- Revalidates initialization bytes, reserved bytes, enum bytes, PAT state,
  precondition state, and compiler-observed POD sizes instead of trusting
  unsafe-copied value types.

The implementation remains value-model and host evidence only. It does not
allocate, populate, install, or mutate a live page table.

## Why

M01-012 and later paging work require one stable definition of levels, entry
kinds, protection, cache policy, address-width rules, hardware-observed bits,
and failure behavior. Encoding those policies before table construction keeps
later planners from inventing incompatible bit conventions or silently
turning invalid addresses and protections into plausible entries.

This step deliberately separates:

- Leaf permission/cache policy from permissive nonleaf traversal permissions.
- Caller-supplied processor observations from proof that the processor is
  actually configured that way.
- Canonical new-entry encoding from strict decoding of hardware-mutated A/D
  bits.
- Pure value transformation from future live synchronization, table
  ownership, CR3 loading, and TLB invalidation.

## Files changed

- `src/OS.Kernel/Memory/PageProtection.cs`
  - Adds the stable `None`, `Read`, `Write`, and `Execute` bit values.
  - Remains a plain enum so the exact source compiles in the no-stdlib closure.
- `src/OS.Kernel/Memory/PageCachePolicy.cs`
  - Adds explicit `Uninitialized`, `WriteBack`, `Uncached`, and
    `WriteCombining` values.
- `src/OS.Kernel/Memory/X64PageTableEntry.cs`
  - Adds levels, entry kinds, A/D policy, status values, PAT token,
    precondition and request PODs, entry/result PODs, strict encode/decode,
    protection-only rewrite, and bounded two-pass batch encoding.
- `tests/OS.HostTests/X64PageTableEntryTests.cs`
  - Adds fourteen self-registering positive, negative, layout, hostile-POD,
    pointer/capacity, no-stdlib, evaluated-EFI-closure, and marker-firewall
    groups.
- `docs/boot.md`
  - Records the public values, exact bit policy, PAT and precondition contract,
    status values, tests, reference influences, limitations, and evidence
    boundary.
- `docs/architecture.md`
  - Records the new host-tested capability without changing a completion gate.
- `done/step858.md`
  - This evidence record.

No EFI entry, EFI project, boot sequence, diagnostics probe, README,
completion-gate, manifest, or reference file changed.

## Public contract

Stable policy values:

```text
PageProtection.None        = 0
PageProtection.Read        = 1
PageProtection.Write       = 2
PageProtection.Execute     = 4

PageCachePolicy.Uninitialized = 0
PageCachePolicy.WriteBack     = 1
PageCachePolicy.Uncached      = 2
PageCachePolicy.WriteCombining = 3
```

Canonical levels and forms:

```text
PTE    leaf  4 KiB
PDE    table pointer or leaf  2 MiB
PDPTE  table pointer or capability-gated leaf  1 GiB
PML4E  table pointer only
```

Present leaves accept only:

```text
Read
Read | Write
Read | Execute
```

All other protection combinations fail. Absence of `Execute` emits NX, so NXE
must be reported enabled. CR0.WP must also be reported enabled. LA57 is
explicitly rejected because this contract defines only the four-level format.

New table pointers use supervisor `P|RW`, clear NX and PS, and use the raw PAT
index-zero WB page-walk encoding. Writable traversal permission does not make a
descendant leaf writable, and executable traversal permission does not make a
descendant leaf executable. The table-page allocation itself must later be
mapped with a separate RW+NX leaf entry.

The PAT token validates all eight raw PAT bytes, reserved bits, reserved memory
types, selected indexes, initialization state, and explicit reserved storage.
Leaf index bits map to PWT, PCD, and PAT. PAT is bit 7 for a 4 KiB PTE and bit
12 for a large PDE/PDPTE.

The precondition POD is value evidence only. It does not execute CPUID or read
CR0, CR4, EFER, or IA32_PAT. A 1 GiB leaf additionally requires an explicit
positive CPUID.80000001H:EDX[26] observation.

Strict decode permits hardware Accessed on every present entry and Dirty only
on leaves. `TryReencodeLeafProtection` is the only API that can intentionally
carry those supplied observations into a replacement value, and its caller
must select `Clear` or `Preserve`.

The batch API accepts request and destination pointers, logical count, complete
declared capacities, and one encoding context. It caps both capacities at 512,
checks compiler-sized spans, alignment, null pointers, overflow, aliasing,
source/destination capacity, and every request before its second-pass writes.
It does not retain pointers and uses no fixed page-sized stack scratch.

The one-byte append-only status contract is:

```text
 0 Uninitialized                       24 UnsupportedEntryBits
 1 Success                             25 InvalidLargePage
 2 RequestNotInitialized               26 DirtyBitOnTablePointer
 3 InvalidLevel                        27 NonCanonicalTablePointer
 4 InvalidEntryKind                    28 InvalidBatchCount
 5 InvalidUnmappedEntry                29 TooManyEntries
 6 InvalidTablePointer                 30 SourceCapacityTooSmall
 7 InvalidProtection                   31 DestinationCapacityTooSmall
 8 WritableExecutableRejected          32 CapacityExceedsLimit
 9 InvalidCachePolicy                  33 MissingSource
10 CachePolicyNotSupported             34 MissingDestination
11 PatMappingNotInitialized            35 MisalignedSource
12 InvalidPatEntry                     36 MisalignedDestination
13 PatIndexOutOfRange                  37 SourceSpanOverflow
14 PatMemoryTypeMismatch               38 DestinationSpanOverflow
15 PreconditionsNotInitialized         39 SourceDestinationOverlap
16 NxeNotEnabled                       40 ContractLayoutMismatch
17 WriteProtectNotEnabled              41 InvalidPatMappingState
18 FiveLevelPagingNotSupported         42 InvalidPreconditionState
19 UnsupportedPhysicalAddressWidth     43 OneGiBPagesNotSupported
20 InvalidPhysicalAddress              44 InvalidRequestState
21 MisalignedPhysicalAddress           45 InvalidHardwareStatePolicy
22 PhysicalAddressOutsideWidth         46 ExistingEntryNotLeaf
23 NonCanonicalUnmappedEntry
```

## Tests run

All commands ran from `C:\Projects\CLROS-M01-011` on 2026-07-26 with .NET SDK
`10.0.302`, branch `codex/M01-011-page-table-entries`, and base commit
`76ba9539cd8b35c66c3b0586599b8890589717d2`.

### Focused production and host-test builds

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

Both commands exited `0` with `0` warnings and `0` errors after the final
implementation and review corrections.

### Complete solution build

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
```

Result: exit `0`; build succeeded with `0` warnings and `0` errors.

### Complete host harness

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Authoritative post-review result: exit `0` in `329066` milliseconds.

Captured ignored evidence files:

```text
artifacts/m01-011-evidence/host-tests.stdout.log
  bytes=389793
  lines=6107
  sha256=54EAABABA30417903504AEA50BB43E0DF9189EAC2B239E3BEAA9CBD03A18A98F

artifacts/m01-011-evidence/host-tests.stderr.log
  bytes=0
  sha256=E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
```

Marker counts:

```text
TEST_START m01.011                         count=1  line=6072
TEST_START m01.011 <numbered group>        count=14 lines=6073-6099 odd
TEST_PASS m01.011 <numbered group>         count=14 lines=6074-6100 even
CLROS_M01_011_READY                        count=1  line=6101
TEST_PASS m01.011                          count=1  line=6102
TEST_FAIL <any>                            count=0
TEST_PASS host.tests                       count=1  line=6107
```

The final issue boundary is:

```text
TEST_START m01.011 14 evaluated efi and marker firewalls hold
TEST_PASS m01.011 14 evaluated efi and marker firewalls hold
CLROS_M01_011_READY
TEST_PASS m01.011
```

The complete captured log terminates with:

```text
TEST_PASS host.tests
```

### Test coverage

The fourteen groups cover:

1. Exact enum values, POD sizes, field offsets, and default states.
2. All 256 raw protection bytes.
3. Explicit unmapped versus malformed/default requests.
4. Exact 4 KiB R, RW, and RX bit patterns and round trips.
5. Canonical nonleaf table pointers.
6. Exact 2 MiB and capability-gated 1 GiB leaves.
7. Level-aware WB, UC, and WC PAT selector placement.
8. Malformed PAT/cache evidence, forged PODs, and the distinction between a
   selected duplicate leaf-WB index and raw PAT index-zero page-walk WB.
9. NXE, WP, LA57, physical width, flag-byte, and reserved-byte preconditions.
10. Zero, default, misaligned, 36/48/52-bit boundary, and above-width
    addresses.
11. Hardware Accessed/Dirty classification, reserved-bit rejection, and
    explicit A/D clear/preserve protection rewrites.
12. Pointer, count, capacity, layout, alignment, overflow, overlap,
    max-512, canary, and whole-batch atomicity behavior.
13. First-party source firewall and exact no-stdlib/zero-reference
    compilation.
14. Evaluated Release EFI closure and marker-surface firewalls for both
    workload profiles.

### Evaluated canonical EFI closures

The host test evaluates the actual Release `Compile`, `ProjectReference`, and
`Reference` items for `SingleMinimalApi` and `MultiMinimalApi`. Each evaluation
requires the canonical EFI source anchors and proves that the three production
contract sources and the host test are absent from the current EFI closure.
The exact contract sources separately compile with:

```text
NoStdLib=true
NOSTDLIB_EFI
framework references disabled
assembly references=0
```

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

This validates runner/profile command construction only. It is not QEMU,
OVMF, EFI, guest, paging, or boot evidence.

### Repository checks

```powershell
git diff --check
git status --short
```

`git diff --check` exited `0`; Git emitted only configured LF-to-CRLF
working-copy notices. Before commit, `git status --short` contained exactly
the two owned documentation changes and five new owned files:

```text
 M docs/architecture.md
 M docs/boot.md
?? done/step858.md
?? src/OS.Kernel/Memory/PageCachePolicy.cs
?? src/OS.Kernel/Memory/PageProtection.cs
?? src/OS.Kernel/Memory/X64PageTableEntry.cs
?? tests/OS.HostTests/X64PageTableEntryTests.cs
```

No generated artifact is tracked.

## Evidence

Final implementation hashes before commit:

```text
C97755EC4395B4854FBCA707601DAB21815D5F59765F4557E0CE8E330A92F125  src/OS.Kernel/Memory/PageProtection.cs
2C6FD33FA96636D01A9E73E76BE34E22B4A512F060C96C2883A787FB6C7D01E5  src/OS.Kernel/Memory/PageCachePolicy.cs
4E48C062E9456E8C122CB094879E74AEBBC1EE94723DCC1C422288FA480F0A11  src/OS.Kernel/Memory/X64PageTableEntry.cs
C0B87A8967AD92CE987B2EFF3D933C6177D1FE3F87BC8F1BCA082C18110879DB  tests/OS.HostTests/X64PageTableEntryTests.cs
4528CD46C8B26097CD6C139F6BC7B9E2111994AABC8F76F7997D87EA784AD758  docs/boot.md
1DA3ADDCF57B96A1107819985DF38DBD4AA718D0B87837B5B9C080E7655339C0  docs/architecture.md
```

Reference use was owner-authorized architectural and test influence. CLROS
did not import a reference project or copy a reference paging implementation,
and reference behavior is not used as completion evidence. The supplied local
snapshot has no recoverable Git revision, so exact paths and content hashes
are the preserved source identity:

```text
DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652  references/SharpOS-main/OS/src/Kernel/Paging/PageFlags.cs
35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC  references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs
B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6  references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs
```

`PageFlags.cs` was used to cross-check architectural bit positions.
`X64PageTable.cs` was used to cross-check address masks and paging levels.
`PagingValidation.cs` influenced independently authored round-trip, duplicate,
neighbor, and malformed-entry categories. CLROS does not adopt the reference
implementation's live ownership, blind address masking, permissive leaf
permissions, or TLB behavior.

Three independent read-only reviews examined:

- Architectural entry semantics, PAT selection, 1 GiB capability handling,
  table-pointer permissions, reserved-bit handling, and future live-use risks.
- Unsafe POD layout, forged state, batch pointer/capacity/aliasing checks,
  failure atomicity, no-stdlib compatibility, and stack usage.
- Test completeness, evaluated EFI closure, marker ownership, documentation,
  reference provenance, and live-evidence boundaries.

All blocking and actionable findings were corrected before the authoritative
run.

## Failures and corrections

- Early focused test development exposed failures in PAT selector expectations,
  no-stdlib fixture composition, and evaluated EFI/marker firewalls. The
  implementation and tests were corrected; no failing result is retained as
  evidence.
- Independent review found that 1 GiB PDPTE leaves were not yet gated on an
  explicit processor-capability observation. The precondition POD now carries
  and revalidates that observation, and missing support returns
  `OneGiBPagesNotSupported`.
- Independent review found that byte-backed flags, reserved POD bytes, and
  unsafe-copied PAT/precondition/request values needed fail-closed
  revalidation. Explicit reserved storage and validation were added across the
  value boundary.
- Independent review found that compile-time layout constants alone were
  insufficient at the unsafe batch boundary. The implementation now checks
  actual compiler-observed sizes before pointer arithmetic.
- The first batch implementation used a fixed page-sized stack scratch buffer.
  It was replaced with a validation-only first pass and deterministic
  direct-write second pass, preserving failure atomicity without the large
  stack allocation.
- Independent review found that canonical table-pointer page-walk caching must
  validate raw PAT index zero independently of a duplicate WB index selected
  for leaf mappings. The contract and a negative forged-PAT test now enforce
  that distinction.
- Independent review found that future M01-014 protection changes require an
  explicit decision about hardware Accessed/Dirty observations. The
  protection-only rewrite now requires `Clear` or `Preserve` and is covered by
  both policies plus malformed-policy rejection.
- A preliminary full-harness command outlived the invoking tool timeout. Its
  exact parent and child processes were inspected and stopped, and the partial
  output was discarded. A later detached run reached the terminal marker but
  did not retain an authoritative process exit code, so it also was not used
  as final evidence.
- The final wrapper captured raw stdout, raw stderr, duration, and exit code.
  It exited `0`, stderr was empty, no `TEST_FAIL` marker occurred, every issue
  marker occurred exactly once at its owning level, and the log ended with
  `TEST_PASS host.tests`.

## Deferred work

- The sources remain outside the canonical EFI compile closure and are not
  called by `EfiMain`.
- No table pages are allocated, reserved, owned, zeroed, mapped, populated, or
  protected.
- No live CPUID, CR0, CR4, EFER, or IA32_PAT state is read or programmed.
- The contract does not account for MTRR interactions with PAT.
- No current boot phase builds a complete hierarchy, proves effective
  descendant permissions, keeps page zero unmapped, maps a page-table arena
  RW+NX, or performs an RW-to-RX transition.
- No live entry is read or written, no synchronization or TLB invalidation is
  performed, CR3 is not loaded, and no page fault is triggered or handled.
- User, global, software, protection-key, LA57, PCID, and TLB-shootdown
  policies remain deferred.
- `DmaMapping` belongs to M05 and is not defined here.
- M01-012 owns the caller-owned identity-mapping plan. M01-014 owns table-image
  mutation and invalidation planning. M01-019 owns the coordinated reversible
  paging phase. Later EFI-link and QEMU issues own any live claim.
- No completion gate, CloudReady claim, Kestrel claim, hosted-.NET claim,
  provider claim, or production-readiness claim changes.

## Evidence boundary

This step proves only that the committed, no-stdlib-compatible CLROS value
contract produces and strictly classifies the documented x64 entry subset for
the finite host corpus; fails closed for the covered malformed policies,
addresses, capability observations, PAT state, entry bits, pointer spans, and
capacity cases; preserves batch destination bytes on validation failure; and
is absent from both current canonical EFI workload-profile closures.

`CLROS_M01_011_READY` is a host-harness marker. It is not EFI, QEMU, OVMF,
live-paging, processor-state, NX/W^X, CR3, TLB, page-fault, or completion-gate
evidence. All such live boundaries remain unproven.
