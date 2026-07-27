# Step 866 - Host-tested reversible owned-CR3 stage-three composition

## Summary

Implemented M01-019 as a no-stdlib-compatible, host-tested third M01
coordinator over the final M01-018 allocator state and the reviewed M01-011
through M01-015 paging contracts. The coordinator cross-binds three distinct
caller-attested allocations, a refreshed memory map, a canonical reservation
ledger, identity-map inputs, four audited x64 byte plans, a complete
caller-owned four-level table image, architectural preflight, reversible CR3
transcript evidence, RW+NX-to-RX mutation/invalidation/probe evidence, and a
page-zero absence audit.

The implementation materializes and reads the bytes but executes none of them;
it does not write CR3, change a processor-owned PTE, execute `invlpg`, invoke
the probe, touch firmware, or run under QEMU. The reversible-CR3 and
protection/invalidation/probe leaves remain separate host models rather than
one temporal machine-code interval. Public capability constants, tests, and
documentation state every nonclaim.

`BootSequencePlan` freezes the exact 20-line stage-three transcript.
`BootSequence.RunM01LivePagingIntegration` is a retrospective host presenter:
it emits that transcript only for a final successful result, emits a bounded
three-line failure transcript otherwise, and leaves the ordinary/default boot
path byte-for-byte unchanged.

## Why

M01-011 through M01-015 established independently tested entry encoding,
identity-map planning, reversible CR3, protection/invalidation/probe, and
page-zero leaves. M01-017 and M01-018 then established loaded-image/owned-stack
and allocator coordinator states. No previous boundary proved that these
results could be composed with exact allocation, map, reservation, storage,
permission, byte-readback, register, canary, restoration, and marker-order
relationships.

The composition also exposed an architectural gap: the existing leaves cannot
prove that one executable interval performed CR3 entry, PTE mutation,
`invlpg`, probe execution, page-zero audit, and complete register restoration.
M01-019 preserves useful host composition evidence while requiring a new
M01-027 composite-byte-plan blocker before EFI linkage or live M01 paging
proof.

## Files changed

- `src/OS.Kernel/Boot/M01LivePagingIntegration.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Boot/BootSequencePlan.cs`
- `tests/OS.HostTests/M01LivePagingIntegrationTests.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/diagnostics.md`
- `done/step866.md`

No change was made to `src/OS/Boot/EfiEntry.cs`, `src/OS/OS.csproj`,
`src/OS.Kernel/Diagnostics/Probes.cs`, EFI/QEMU scripts,
`docs/completion-gates.md`, `README.md`, or any reference snapshot.

## Tests run

All commands were run from `C:\Projects\CLROS-M01-019` against base commit
`0329b4de7e1d3ba5039c533357cd47eec6d899dd`:

```text
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1 --disable-build-servers
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --disable-build-servers
  exit 0; Build succeeded; 0 warnings; 0 errors

focused M01-019 runner
  exit 0
  all 14 named M01-019 groups passed
  TEST_PASS m01.019 identity requirements reject caller relabeling
  TEST_PASS m01.019 exact source closure compiles under nostdlib efi
  CLROS_M01_019_READY
  TEST_PASS m01.019

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  exit 0 in 446.1 seconds
  6,327 output lines
  terminal marker: TEST_PASS host.tests

dotnet build .\CLROS.slnx --nologo -m:1 --disable-build-servers
  exit 0; Build succeeded; 0 warnings; 0 errors

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
  exit 0
  TEST_PASS naming.guard
  TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
  exit 0
  TEST_PASS workload.profile.tooling default single
  TEST_PASS qemu.runner.dryrun

git diff --check
  exit 0; no whitespace errors
```

The full harness was serialized after all source and tests were frozen. An
independent adversarial review then rebuilt the complete solution with zero
warnings/errors and found no merge-blocking defect.

## Evidence

The stage-three coordinator requires and cross-binds:

- a final M01-018 static state whose embedded stage-one state remains at
  `ContinuationRspContained`, with the exact reviewed switch plan;
- three sequential, successful caller-attested EFI allocations: one page of
  `LoaderCode` for preflight/CR3/invlpg bytes, one distinct `LoaderCode` page
  for the protection probe, and exact `LoaderData` pages for the calculated
  table count plus one state/canary page;
- changed map key, exactly incremented map generation, strict allocation/map/
  ledger ordinals, exact descriptor accounting, and an exact refreshed ledger;
- unique CR3-transition, page-protection-probe, table-state, loaded-image,
  allocator, ACPI-scratch, and framebuffer-metadata reservations plus retained
  stage-one switch-stub, guard, and stack reservations;
- complete pairwise disjointness across all 17 caller-owned metadata, map,
  code, probe, table/state, transcript, and observation spans;
- one exact mapping requirement per loaded-image page plus ten non-image
  requirements. The loaded image is tiled without gaps/duplicates: only the
  continuation page is RX, other image pages are read-only/NX, and the exact
  handoff page envelope uses the same overlapping policy;
- exact stack RW+NX, guard absent, stage-one switch stub RX, ACPI/framebuffer
  read-only, CR3 code RX, probe initially RW+NX, complete table/state RW+NX,
  and allocator RW+NX mappings, all using write-back RAM policy;
- exact readback of preflight, reversible-transition, `invlpg`, and
  constant-return-probe bytes, including zero padding outside assigned ranges;
- architectural preflight requiring PG, WP, PAE, LME, LMA, and NXE while
  rejecting DF, LA57, PCIDE, invalid physical width, or an outside root;
- exact raw firmware CR3 low bits, raw CR4, RFLAGS/IF, PGE clear/readback and
  restoration/readback, callback exclusion, bounded timing, continuation
  canary, stack/RSP, and event transcript evidence;
- one exact RW+NX-to-RX modeled probe-leaf mutation, exact target
  invalidation/readback, expected constant return, and page-zero absence; and
- restoration of the modeled probe leaf to its original RW+NX value on success
  and rejected aggregate paths. The newly constructed table image remains the
  output, so the source correctly reports that it does not restore the entire
  caller buffer to its pre-call bytes.

`VerifyExisting` is read-only for all four byte plans: it validates existing
bytes and synthesizes the expected materialization POD without calling an emit,
materialize, or zeroing routine. The isolated 29-file production closure
compiles with `/nostdlib+`, `NOSTDLIB_EFI`, C# 14, no framework references,
warnings as errors, and deterministic output. Metadata inspection finds no
assembly references, module references, P/Invoke imports, or native source.
`BootSequence.cs`, `EfiEntry.cs`, and `Probes.cs` are explicitly excluded from
that closure.

Negative coverage includes default/forged states; every partial allocation
cleanup permutation; wrong EFI status/type/range/ordinal; stale map and
generation; malformed descriptor accounting; missing/duplicate/overlapping
reservations; arbitrary or relabeled identity ranges; missing/duplicate image
tiles; pointer, count, capacity, alignment, wrap, and alias failures; corrupted
or padded stub bytes; unsupported paging modes; wrong CR3/CR4/RFLAGS/PGE;
executable table data; writable code; mapped guard/page zero; wrong mutation or
`invlpg` target; execute-before-protect; callback/interrupt violations;
restoration, canary, timeout, transcript, and presenter-marker failures; and
duplicate coordinator execution.

The success presenter is exactly:

```text
TEST_START m01.stage3
M01_CR3_STUB_PAGE_ALLOCATED
M01_PROTECTION_PROBE_PAGE_ALLOCATED
M01_PAGE_TABLE_STATE_ARENA_ALLOCATED
M01_STAGE3_MAP_RECAPTURED
M01_CR3_STUB_MATERIALIZED
M01_PAGE_TABLES_BUILT
M01_PAGING_MODE_VALIDATED
M01_PRE_CR3
M01_FIRMWARE_CR3_RESTORED
M01_RFLAGS_RESTORED
M01_PGE_CYCLE_VERIFIED
M01_OWNED_CR3_CANARY_VERIFIED
M01_POST_CR3
M01_RW_TO_RX_VERIFIED
M01_TLB_INVALIDATION_COMPLETE
M01_RX_PROBE_EXECUTED
M01_PAGE_ZERO_ABSENT
TEST_PASS m01.stage3
CLROS_M01_LIVE_BOOT_MEMORY_READY
```

No line is emitted while the owned interval is modeled active. The presenter
reports already completed host evidence; its ordering is not a processor
execution transcript.

Stable pre-commit SHA-256 hashes:

```text
650E81A0250C7D3C561909657745B0BC617D59B7D03743C0E1932688FB1EE116  src/OS.Kernel/Boot/M01LivePagingIntegration.cs
8829424E3007E938D23F757C8BC4BB24EC1F5DE1CB513FA1D2F0E95707041A17  src/OS.Kernel/Boot/BootSequence.cs
32FA9290D8C7E5A2657907038FA2F275BFF5E83BA3EE33035D1F0A90B4B7A5CD  src/OS.Kernel/Boot/BootSequencePlan.cs
262A49EE6B110E6233C2AFA91C2AB05BCBBFE3071BD825C9F559493A340AB905  tests/OS.HostTests/M01LivePagingIntegrationTests.cs
9C2491D5D8C8204C402C1165072F8E9EF353E3593C036AFA9CDB7757972A46F0  tests/OS.HostTests/BootSequencePlanTests.cs
FC0A57699630C322B9AD70EF3920651E1AFE0A06E6A5DDECF0CDF3168C13671B  docs/boot.md
3C7281A130257858E7F5CD6F20A2309DAE1E98A3FA707E3973DB491FBF1F337A  docs/architecture.md
2F07CB57FC555B2CEEF3B0FC0257954EDEB004A723B8D2465AA30F49EA6532E4  docs/diagnostics.md
```

Reference influence was architectural only:

- `C:\Projects\CLROS\references\SharpOS-main\OS\src\Kernel\Paging\X64PageTable.cs`
  (SHA-256
  `35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`)
  was used to cross-check hierarchy/transition concepts.
- `C:\Projects\CLROS\references\SharpOS-main\OS\src\Kernel\Paging\Cr3Accessor.cs`
  (SHA-256
  `088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47`)
  was used to cross-check the CR3 boundary.
- No SharpOS source, permanent mapping policy, firmware callback, raw pointer
  ABI, or live-execution claim was copied. Source study is owner-authorized;
  the snapshot lacks an independently recovered upstream revision, so exact
  file hashes are recorded instead of invented provenance.

## Failures

Several defects were found and corrected before the final evidence run:

- the first implementation searched the wrong SharpOS hash and was corrected
  to the exact inspected `X64PageTable.cs` digest;
- allocation ordinals accepted an overflow edge and were tightened to three
  nonzero, non-maximum, strictly sequential values;
- stage-one state and switch-plan relationships were initially under-bound;
- metadata alias checks did not initially cover every complete caller capacity;
- the table-state containment check compared a virtual pointer with a physical
  endpoint; it now uses the checked virtual allocation envelope;
- cleanup initially failed to reject overlapping completed allocations;
- the first table audit checked only one table/state page and incomplete
  stage-one code/stack/guard coverage; every relevant page is now audited;
- byte-plan order initially permitted table construction before all four stub
  readbacks;
- `VerifyExisting` initially reused emit/materialize paths and could mutate the
  caller's byte pages; all four branches are now read-only;
- a wrong probe-mutation target could return without restoring the modeled
  leaf;
- the initial success fixture trusted caller purpose labels and used arbitrary
  handoff, ACPI, and framebuffer pages; exact reservation/role cross-binding,
  page tiling, and hostile relabeling tests now fail closed;
- whole-image RX and whole-buffer-restoration claims were narrowed to actual
  role-based page permissions and probe-leaf restoration; and
- concurrent review builds briefly removed intermediate reference assemblies.
  Those superseded runs were discarded, build servers were stopped, and every
  authoritative gate was rerun serially from the frozen snapshot.

The final full harness, common repository gates, and adversarial review passed.

## Deferred work

- M01-027 must replace the separate models with one no-stdlib-compatible,
  audited composite owned-CR3 byte plan before M01-020 source linkage or any
  M01-025/M01-026 live/QEMU claim. It must bind every actually dereferenced
  interval range and perform CR3 entry, canary update, PTE rewrite, exact
  `invlpg`, probe, page-zero audit, and CR3/CR4/RFLAGS restoration in one
  callback-free stream.
- The frozen `SerialStub` purpose is bound to the reviewed stage-one
  `BootStackSwitchStub` only as a compatibility alias that prevents arbitrary
  RX input. `CrossBindsOwnedSerialStub` and
  `ProvesOwnedSerialDiagnostics` remain `false`; M02 owns real port-I/O/COM1
  allocation, materialization, invocation, and evidence.
- ACPI-scratch and framebuffer requirements prove only exact same-kind
  reservation-ledger coherence, not copied contents or firmware provenance.
- Owned map identity, diagnostic-state identity, PE-section permissions,
  PCID/LA57, NMI/SMI safety, deliberate null dereference, live allocation,
  EFI linkage, and QEMU execution remain deferred.

## Evidence boundary

- This is host evidence over caller-owned pre-EBS records, bytes, tables, and
  transcripts.
- No firmware allocation, cleanup call, callback, privileged instruction,
  generated-code invocation, installed-table read, CR3 load, live PTE change,
  TLB invalidation, probe execution, or null dereference occurred.
- The coordinator is not linked into `src/OS`, called from `EfiEntry`, enabled
  by `Probes.cs`, or used by the default boot path.
- The QEMU command was a dry run; QEMU/OVMF did not execute and no guest marker
  was observed.
- `CLROS_M01_019_READY`, the 20-line `m01.stage3` transcript, and
  `CLROS_M01_LIVE_BOOT_MEMORY_READY` are reserved host-contract evidence only.
  They do not advance `docs/completion-gates.md`, establish a single composite
  machine-code interval, prove M01 complete, or support CloudReady/production
  claims.
