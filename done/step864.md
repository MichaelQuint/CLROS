# Step 864 - Ordered loaded-image and owned-stack integration

## Summary

Implemented M01-017 with a host-tested, allocation-free coordinator that joins
the existing M01 loaded-image, allocation, refreshed-memory-map, reservation,
owned-stack, switch-stub, and one-shot transition contracts into one fail-closed
stage-one plan.

The coordinator requires explicit physical and caller-attested identity-mapping
evidence for the loaded image, loader-code page, loader-data arena, and owned
memory-map destination. It validates the complete reservation topology and
virtual alias matrix before materializing an audited stack-switch stub and
arming caller-owned static transition state.

The default boot path remains unchanged. A separate host presenter owns the
exact `m01.stage1` marker transcript, and only the small static-state transition
APIs remain in the `NOSTDLIB_EFI` surface. The large host audit result is never
required after the planned stack transfer.

## Why

M01-003 through M01-005 and M01-012 through M01-016 supplied individually
tested contracts, but no integration boundary proved that their address
domains, reservations, owned storage, and transition states compose safely.
M01-017 establishes that ordered host-side boundary without prematurely wiring
`EfiEntry`, allocating firmware pages, executing a stack switch, or claiming
guest evidence.

This slice also prevents a subtle class of unsafe integrations: a physically
valid allocation can still alias the loaded image, owned map, static state,
continuation, allocation table, or reservation ledger in the virtual address
domain. Explicit mapping evidence and checked end-exclusive comparisons make
those relationships reviewable and reject overflow before any range is used.

## Files changed

- `src/OS.Kernel/Boot/M01LoadedImageStackIntegration.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Boot/BootSequencePlan.cs`
- `tests/OS.HostTests/M01LoadedImageStackIntegrationTests.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot.md`
- `done/step864.md`

## Tests run

All commands were run from `C:\Projects\CLROS-M01-017` against base commit
`4373d476ee3d58d231772616616c48ad4a5a1991`:

```text
dotnet build .\CLROS.slnx --nologo -m:1
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  exit 0
  line 2202: TEST_START m01.017
  line 2247: CLROS_M01_017_READY
  line 2248: TEST_PASS m01.017
  line 6245: TEST_PASS host.tests
  zero TEST_FAIL or OS_SMOKE_FAIL markers
  standard error was empty

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
  exit 0
  TEST_PASS naming.guard
  TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
  exit 0
  TEST_PASS workload.profile.tooling default single
  TEST_PASS qemu.runner.dryrun

git diff --check
git diff --cached --check
  exit 0; no whitespace errors
```

The final full host-harness run produced 397,313 bytes of standard output and
zero bytes of standard error. Its relevant markers and the terminal
`TEST_PASS host.tests` each occurred exactly once.

## Evidence

The integration result is a pre-transfer audit/report POD. It records every
accepted input and checkpoint, but it is deliberately not part of the live
transition context. The caller-owned static transition structure retains only
the handoff, switch state, stack layout, and target information needed across
the planned transfer.

The preparation path verifies:

- loaded-image capture plus matching physical and virtual identity evidence;
- exactly one loader-code page and one separate loader-data arena;
- allocation provenance and a refreshed owned-memory-map ledger;
- a changed opaque map key, incremented generation, and owned destination
  metadata;
- canonical loaded-image, stub, guard, and owned-stack reservations;
- an exact one-page guard and stack lifetime topology;
- full physical coverage of the map destination by the loaded image or one
  exact owned-memory-map reservation;
- explicit physical-to-virtual identity bindings for code, data, and map
  storage;
- checked range ends and rejection of aliases involving code, data, image,
  map, static state, continuation, allocation table, and reservation ledger;
- audited stack-switch byte-plan emission and exact readback;
- fixed static-state offsets, alignment, containment, and one-shot arming; and
- exact continuation `RSP` equality and containment when the static transition
  audit is advanced.

`PrepareWithStubByteMode` keeps verification-only mode negative for this stage:
even matching pre-existing bytes cannot be promoted to a prepared result
without this coordinator owning materialization. The production coordinator
does not invoke firmware, an unmanaged target, a processor stack switch, CR3,
TLB operations, or page-protection changes.

The host tests cover default values; integer/range overflow; null, unaligned,
short, over-capacity, reordered, duplicate, overlapping, and mismatched
records; stale map and owner evidence; invalid guard/stack topology; every
relevant virtual alias; byte-plan mismatch; repeated and illegal transition
states; escaped/misaligned continuation `RSP`; returning-target classification;
nonmutation on rejected inputs; and deterministic repeated planning.

The exact no-stdlib compiler closure and PE metadata inspection prove that:

- `Prepare` and `PrepareWithStubByteMode` remain available;
- `RecordTransferFromStaticState`,
  `ObserveContinuationRspFromStaticState`, and
  `ReportUnexpectedReturnFromStaticState` remain available;
- host convenience overloads carrying the large audit result and their
  `StaticStateMatches` helper are absent;
- no assembly reference, module reference, import mapping, P/Invoke method, or
  external runtime metadata enters the closure; and
- the coordinator has no direct `UefiMemoryType` dependency.

The opt-in presenter emits this exact retrospective success order:

```text
TEST_START m01.stage1
M01_LOADED_IMAGE_CAPTURED
M01_STACK_SWITCH_STUB_READY
M01_STACK_ARENA_ALLOCATED
M01_STACK_GUARD_RESERVED
M01_STAGE1_MAP_RECAPTURED
M01_STAGE1_RESERVATIONS_VALIDATED
M01_OWNED_STACK_READY
M01_OWNED_STACK_SWITCHED
M01_RSP_CONTAINED
TEST_PASS m01.stage1
```

An ordinary failure emits only the start, a status/checkpoint
`TEST_FAIL m01.stage1`, and `M01_STAGE1_FAIL`; later success markers are
suppressed. A nonfinal result carrying `Success` is reclassified as
`IllegalCoordinatorState`. The default five non-smoke and eight total
`BootSequence.Run` lines remain byte-for-byte unchanged.

Final log hashes:

```text
A02F0B87DED8E440C36045DBA18A3CF8E5B81E2AC20D4736FFA645A4D064CDF1  artifacts/m01-017-solution-build.log
F5E813DF968A954B47BFA29DC3FEBA358B4E90B52D74C5E7D487143B26810879  artifacts/m01-017-hosttests-build.log
04C79E83AA18B8988B4C8E79C23929CF7217938470EF9CDEBEDF3B90E916CD5A  artifacts/m01-017-hosttests-6.out.log
E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855  artifacts/m01-017-hosttests-6.err.log
FB9E2F9DEB178474FD6713AA598348A053B28FC79CA27732159CDD0AC565CE18  artifacts/m01-017-check-naming.log
FD277F0955964B3A7F92EEC4BF56E94E53A5AE520F166F52EC71E4EECB92005B  artifacts/m01-017-qemu-dryrun.log
```

Reference influence:

- `C:\Projects\CLROS\references\SharpOS-main\OS\src\Boot\BootStackSwitch.cs`
  was used only as an architectural reference for one-shot setup before
  transfer, static handoff ownership, nonreturning continuation intent, and
  post-switch stack bounds.
- The inspected snapshot file has SHA-256
  `637DCFC91E3B9647A0F8FBD4800E6C973ED78C6E5F73E2F7C10770C000234808`.
- No raw firmware pool pointer, global Boolean, direct platform coupling,
  generated patcher, or returning-to-address-zero behavior was copied.
- Direct study is owner-authorized and corroborated by the repository's
  recorded CC0 grant; the local snapshot's exact upstream revision remains
  unknown, so that limitation is preserved rather than inventing provenance.

Independent adversarial reviews found no remaining P0, P1, or P2 issue. They
confirmed the full address-domain and alias checks, the pre-dereference table
preflight, the static-only transition surface, the marker/default-path
isolation, and the exact no-stdlib metadata boundary.

## Failures

Several failures were encountered and corrected before the final evidence run:

- The initial coordinator design lacked explicit virtual-address provenance
  and could not safely compare physical allocation records with virtual code,
  map, state, and continuation addresses. Separate loaded-image and stage-one
  identity-mapping evidence plus a complete checked alias matrix fixed it.
- An earlier diagnostic run exposed a static-transition validation mismatch;
  the live-facing state checks were separated from the host audit result.
- The first no-stdlib closure after guarding host wrappers still generated
  probe calls to those excluded overloads and produced a field-initialization
  warning. The generated probe now calls only static-state APIs, and the
  observed-RSP field is explicitly initialized.
- A later review found that the new presenter itself was not invoked by tests.
  Exact success, ordinary failure, illegal-success, null-output, and default
  isolation tests were added before final run 6.

All superseded diagnostic runs were discarded as completion evidence. Final
run 6 and every common repository gate passed.

## Deferred work

- M01-018 owns the live-allocator stage coordinator and one allocate/free/
  double-free host integration.
- M01-019 owns reversible owned-paging integration.
- M01-020 owns the canonical EFI project linkage. Its current additive source
  list will need to include the established `UefiMemoryType` dependency used by
  `OwnedBootStackSwitchPlan`; this issue did not own `OS.csproj`.
- M01-021 through M01-026 own `EfiEntry` serialization, live firmware page
  allocation, actual stack transfer, guard-page enforcement, page protection,
  and cumulative QEMU/OVMF evidence.

## Evidence boundary

- This proves host-side composition and no-stdlib compilation/metadata only.
- The coordinator is not called by the default boot path or `EfiEntry`.
- No EFI image was rebuilt or inspected for this issue.
- The QEMU command was a dry run; QEMU/OVMF did not execute.
- No live `AllocatePages`, memory-map recapture, reservation subtraction,
  firmware exit, stack transfer, processor `RSP` observation, inaccessible
  guard, W^X/NX transition, paging activation, or guest marker occurred.
- The identity mappings are explicit caller attestations, not page-table
  observations.
- `CLROS_M01_017_READY` and the `m01.stage1` transcript are host presentation
  evidence only. They do not close a completion gate or prove any later M01,
  M02, real-firmware, or cloud boundary.
