# Step 867 - Audited composite owned-CR3 transition leaf

## Summary

Implemented Gitea issue M01-027 as one no-stdlib-compatible, host-audited
Windows-x64 byte stream that replaces the temporal gap between M01-019's
separate paging models. The stream performs architectural and caller-state
preflight, captures the exact raw entry state, executes `cli`, clears PGE,
enters a distinct owned CR3, updates the continuation canary, writes the probe
leaf from RW+NX to the RO+X candidate, executes one `mfence` and one exact
`invlpg`, verifies committed PTE readback, invokes one exact six-byte
constant-return probe, audits page zero through the owned hierarchy, and
converges on one ordered firmware-CR3/CR4/RFLAGS-intent recovery tail.

The implementation is a C#-authored plan and audit boundary. Host tests
materialize and decode its bytes as data; no test invokes privileged code.
The source is not linked into `src/OS`, does not change a processor register or
PTE, does not run in EFI/QEMU, and does not advance a completion gate.

## Why

M01-019 proved that the existing paging, invalidation, probe, and page-zero
leaves could be composed as host data, but it explicitly could not prove that
one executable interval enforced their required temporal order. M01-027
provides that single-stream leaf before M01-020 can link it, M01-025 can
allocate/wire it, or M01-026 can execute it under QEMU.

The new boundary also prevents a copied or relabeled success object from
becoming composite evidence. Every result phase is explicit, and the final
observed-state audit requires the exact immutable `ReadbackVerified` result,
full-page fingerprint, entry address, instruction count, semantic count, and
prepared-state snapshot. Compatibility and separate-leaf inputs are
deliberately non-promotable.

## Files changed

- `src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs`
- `tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step867.md`

No change was made to `src/OS/Boot/EfiEntry.cs`, `src/OS/OS.csproj`,
`src/OS.Kernel/Boot/M01LivePagingIntegration.cs`,
`src/OS.Kernel/Boot/BootSequence.cs`,
`src/OS.Kernel/Boot/BootSequencePlan.cs`,
`src/OS.Kernel/Diagnostics/Probes.cs`, EFI/QEMU scripts,
`docs/completion-gates.md`, `README.md`, or any reference snapshot.

## Tests run

All commands were run from `C:\Projects\CLROS-M01-027` on branch
`codex/M01-027-composite-owned-cr3`.

```text
fresh isolated M01-019 + M01-027 + M01-014 focused runner
  exit 0
  CLROS_M01_019_READY
  TEST_PASS m01.019
  all seven named M01-027 groups passed
  CLROS_M01_027_READY
  TEST_PASS m01.027
  CLROS_M01_014_READY
  TEST_PASS m01.014
  TEST_PASS m01.027.focused.host

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  native process exit 0
  6,345 captured output lines, including the wrapper exit record
  exactly one TEST_START m01.027
  exactly one CLROS_M01_027_READY
  exactly one TEST_PASS m01.027
  zero TEST_FAIL markers
  terminal marker: TEST_PASS host.tests
  wrapper marker: CLROS_FULL_EXIT_CODE 0

dotnet build .\CLROS.slnx --nologo -m:1 --disable-build-servers
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --disable-build-servers
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

The authoritative repository-wide run was serialized after production, tests,
documentation, and this done step were frozen. Its 403,544-byte stdout capture
has SHA-256
`8C3C472808003D7D887F35670CB92DF5E6A4E252AF2D2D2EDF41AAEDCA2D8CE4`.
The 616-byte stderr capture has SHA-256
`2CE70D6C8C9AD6726C794690C8B14A2B83673648CAC88EDF4FF1ACC50EC46348`
and contains only PowerShell's CLIXML “Preparing modules for first use”
progress records, with no error text. The generated logs were verification
artifacts and were not added to Git.

## Evidence

The frozen byte plan contains:

```text
instruction bytes                 3830
audited instructions               732
materialized page bytes           4096
semantic records                    16
full-page FNV-1a64    F00446F61BE13CDD
full-page SHA-256     C70C7BDD0B4E5F877074C2C881F67B079B5165D6F7D48EA1B8E3E2011B1F24BC
prefix SHA-256        12CADD81A9C5EFB9D47AAFEA49988D271656216140B87FF1AE3E0224567F5387
```

Preparation binds the exact prior M01-019 success state and its allocation,
reservation, identity-map, table-image, protection-mutation, invalidation,
probe-readback, and page-zero evidence. Exact role/provenance/index checks
bind the composite code page, stack, loaded-image continuation page,
protection-probe page, complete table/state arena, runtime state, canary, and
owned root. The source uses complete pointer/count/capacity spans, checked
arithmetic, canonical-address checks, disjointness rules, exact typed
reservations, and strict RO/RW/RX/NX/cache-policy relationships.

The complete 4 KiB destination is deterministically zeroed only after all
preflight succeeds. Readback verifies the instruction prefix, every semantic
record, and the entire zero tail. A forged prepared state cannot redirect that
write: the exact bound code-page geometry, entry, state self-address, canary
address, table/state arena, and owned root are revalidated before mutation.

The stream's mutable-state preflight rejects malformed code/table/root
geometry, a same-root no-op, invalid CR3 low bits, unsupported physical width,
missing CR0.PE/PG/WP, missing CR4.PAE, LA57, PCIDE, active CR4.CET, missing
EFER.LME/LMA/NXE, DF, TF, invalid Windows-x64 entry alignment, stack escape,
return-address mismatch, malformed probe bytes, and invalid canary values
before `cli` or privileged table use.

The probe hierarchy is bound by immutable packed PDPT/PD/PT child-page
ordinals and exact per-level indices. Parent entries must be present, writable,
non-large, supervisor, WB, and inside the owned arena before dereference.
Hardware Accessed/Dirty evolution may only move from zero to one. The final
PTE preserves the physical page, cache policy, and any monotonic A/D bits while
clearing writable/NX. `ProbePteAfter` and
`CommittedProbePteReadback` must be bit-for-bit equal because the emitter
stores both from the same unchanged register. The stream performs exactly one
target `invlpg` and one internal indirect transfer to the exact six-byte
`mov eax, 0x4F534F4B; ret` probe.

The runtime transcript distinguishes write attempted from commit verified,
records a historical progress checkpoint separately from the final recovery
checkpoint, and validates the exact cause plus required/forbidden fields for
each failure status. Firmware-CR3 and CR4 restoration use exact hardware
readbacks. RFLAGS is represented honestly as the saved final restoration
operand and the frozen final `popfq; ret` encoding after all nonvolatile
registers are restored; there is no post-`popfq` hardware-readback claim.

The observed audit requires an immutable phase-three readback result. It
rejects default, state-prepared, materialized, foreign, forged, or
separate-leaf proof, and it classifies returned, timeout, reset, and
triple-fault terminals without allowing them to promote into success. Stable
capability flags keep every firmware, console, SerialIo, COM1, allocator,
ACPI, framebuffer, diagnostic, arbitrary-callback, host-execution,
live-register, live-PTE, live-TLB, EFI-linkage, QEMU, and gate claim false.

The focused suite independently decodes the emitted x64 control-flow graph and
ABI rather than treating regeneration as readback. It freezes exact bytes,
hashes, semantic offsets, branch destinations, prologue/epilogue, Win64 shadow
space, nonvolatile GPR/XMM policy, privileged-opcode counts, one call, one
`invlpg`, final `popfq; ret`, callback absence, complete zero tail, corruption
spans, every public status, hostile ranges, malformed table chains, exact
cleanup precedence, and phase anti-promotion. A separately compiled exact
source closure uses `NOSTDLIB_EFI`, C# 14, warnings as errors, no framework
references, and reports zero assembly references, module references, P/Invoke
imports, or native source.

Reference influence was architectural and independently reimplemented:

- `references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`
  (SHA-256
  `B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500`)
  informed the C# x64 byte-plan design.
- `references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs`
  (SHA-256
  `35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`)
  informed hierarchy cross-checks.

No reference source was copied into the first-party implementation. Reference
study is owner-authorized; the snapshot does not provide a recoverable
upstream revision, so exact file hashes are recorded instead of inventing
provenance.

Stable pre-commit SHA-256 hashes:

```text
0CED2E51C1CC18592F15D2908D6F706D54F26B06C155DD0D725466FB78714B38  src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs
7372A2E9DD33B68EF002F1F78739FB26B8C423EF66132A313D0E2F0A452050D5  tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs
6155D0223B18A585441A2EC0DD80CF0CFE5A4099F3902A04A34293B2F3CD491C  docs/boot.md
A4F389C11CEC9963778AB0DFF9B2BE4A7455666D30491EC61CFA4BC0083BE192  docs/architecture.md
```

## Failures

The implementation deliberately remained unregistered until its complete
focused contract passed. Adversarial review and executed tests found and
corrected, before the final evidence run:

- a 72-byte assertion for the actual 80-byte range-binding POD;
- logical-count-only alias checks instead of complete capacity spans;
- role/provenance relabeling and incomplete prior-result index binding;
- use of the full physical-width mask where a page-base mask was required;
- parent walks that could follow non-present or read-only entries;
- an inverted page-zero leaf classification;
- an entry-stack check that accepted any nonzero low nibble instead of
  `RSP % 16 == 8`;
- incorrect EDX:EAX reconstruction after `rdmsr` and missing CR4.PAE;
- unconditional PGE reporting and missing probe-range validation before
  dereference;
- canary validation that accepted any changed value instead of the exact
  replacement;
- stale instruction bytes in the unused code-page tail;
- overflowable pre-write code-page geometry;
- result phases that allowed prepared/separate-leaf evidence to promote;
- an observed-audit baseline/live-pointer contradiction;
- unwritten committed-PTE state and exact-parent-chain gaps;
- mutation-commit claims before committed readback;
- a temporary RFLAGS interrupt window and a false post-`popfq` readback claim;
- cleanup checkpoints that erased the actual failure stage;
- runtime failure statuses accepted without their exact failure predicate;
- cleanup overrides accepted with impossible later-stage output;
- CLI audit that incorrectly required arithmetic flags to match entry flags;
- same-root transitions, TF, and CET/IBT prerequisites that were not explicit;
- stale SHA-256 vectors after the emitter changed;
- a generic `unmanaged` helper and framework numeric constants that broke the
  exact no-stdlib closure;
- an identity-map capacity bound smaller than M01-019's canonical 512-record
  result;
- overly strict prior-PTE comparison that rejected monotonic hardware A/D
  updates;
- imprecise short-readback and CR4-restoration status classification;
- cleanup paths that required a canary read the emitter intentionally skipped;
- test firewall self-matches, stale build races, sealed-address fixture
  rebinding, rel32 corruption-span assumptions, and incomplete fixture mode
  bits; and
- concurrent diagnostic builds that briefly removed intermediate assemblies.
  Those runs were stopped or discarded, build servers were disabled for the
  authoritative builds, and the final focused and repository-wide runs were
  serialized from the frozen files.

## Deferred work

- M01-020 owns explicit EFI source linkage. This step does not add the source
  to `src/OS/OS.csproj`.
- M01-025 owns live allocation, default-off `EfiEntry` wiring, and the
  caller-side pre-transfer proof that TF is clear and supervisor CET/IBT is
  inactive. An in-stream guard cannot prevent a fault on an indirect entry,
  and the exact six-byte probe intentionally lacks `endbr64`.
- M01-026 owns cumulative real-QEMU execution evidence. No QEMU/OVMF guest run
  was performed for this host-only issue.
- M02 owns NMI/SMI-safe descriptors, fault handling, and owned COM1.
- The source does not prove copied firmware-map identity, owned diagnostic
  state, ACPI/framebuffer provenance, PE-section provenance, PCID/LA57, SMP
  shootdown, or deliberate null-page fault handling.

## Evidence boundary

- This is host evidence over caller-owned bytes, POD state, and a modeled page
  hierarchy.
- No generated byte was called. No control register, processor-owned PTE, TLB,
  firmware service, serial port, or guest state was touched.
- The source is absent from the evaluated `src/OS` closure and default boot
  path.
- The QEMU command was a dry run; no QEMU/OVMF guest marker was observed.
- `CLROS_M01_027_READY` and `TEST_PASS m01.027` mean that the host planning,
  materialization, independent decoding, readback, and audit contract passed.
  They do not mean the interval executed and do not advance M01 or any
  completion gate.
