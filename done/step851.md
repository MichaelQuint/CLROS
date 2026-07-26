# Step 851 - M01-004 owned boot-stack layout and lifetime contract

## Summary

Implemented the host-tested, no-allocation `OwnedBootStackLayout` contract for
Gitea issue M01-004. The leaf consumes exactly two caller-owned canonical
reservations through an unsafe pointer/count/capacity boundary, reuses
`BootReservationTableValidator`, and proves that a guard plus usable-stack
range exactly partitions an explicit typed physical backing arena. It preserves
an explicit 16-byte-aligned exclusive physical stack top and models immutable
`Reserved -> Ready -> Active` transition edges.

The implementation and its focused tests compile in an exact
`NOSTDLIB_EFI`/`NoStdLib=true` fixture with all framework references removed.
The emitted closure has zero assembly references. The leaf is not linked into
the canonical EFI project and its readiness marker is emitted only by the host
harness.

## Why

The existing boot-reservation ledger could validate a supplied guard/stack
pair, but CLROS had no canonical value that bound that pair to explicitly owned
backing, recorded the exclusive physical top, or represented pre-switch and
post-switch ordering. M01-004 supplies that reusable contract without crossing
the later allocation, paging, executable-stub, `RSP`, or live-firmware
boundaries.

The implementation deliberately reuses the canonical ledger validator instead
of defining competing overlap/order rules. `PhysicalStackTop` remains a
`PhysicalAddress`; M01-005 must separately derive and validate a virtual switch
target and complete x64 frame/home-space planning.

## Files changed

- `src/OS.Kernel/Boot/OwnedBootStackLayout.cs`
  - Adds append-only byte-valued layout/lifetime statuses.
  - Adds a 128-byte unmanaged layout and a 176-byte unmanaged evidence result.
  - Checks exact-two table metadata, typed backing arithmetic, physical-top
    alignment/equality, canonical guard/stack topology, exact accounting, and
    defensive malformed-value revalidation.
  - Models successful `Reserved -> Ready` and `Ready -> Active` edges and
    classifies same-state, skipped, reversed, default, and malformed requests.
- `tests/OS.HostTests/OwnedBootStackLayoutTests.cs`
  - Adds the focused M01-004 module-initializer suite and stable markers.
  - Covers POD/ABI shape, defaults, high addresses, pointer metadata and span
    wrap, zero/undersized/overflowing backing, zero guard/usable extents,
    malformed canonical records, gaps/overlaps/order, exact backing and top,
    nested validator evidence, legal/illegal transitions, stale-snapshot
    behavior, malformed results, deterministic precedence, source
    immutability, zero allocation, canonical-EFI exclusion, marker location,
    and the exact no-stdlib compiler closure.
- `docs/boot.md`
  - Documents the contract, stable focused markers, reference provenance,
    caller-attested lifetime semantics, stale-snapshot limitation, and exact
    evidence boundary.
- `docs/architecture.md`
  - Adds the owned-stack layout to implemented host-only behavior, tests, and
    known limitations without promoting a completion gate.
- `done/step851.md`
  - Preserves this implementation and evidence record.

## Tests run

All commands ran from `C:\Projects\CLROS-M01-004`.

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
git status --short
```

Results:

- Kernel build: succeeded with 0 warnings and 0 errors.
- Solution build: succeeded with 0 warnings and 0 errors.
- Host-test build: succeeded with 0 warnings and 0 errors.
- Complete host harness: final `TEST_PASS host.tests`.
- Naming/source firewall: `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- QEMU command planning: `TEST_PASS qemu.runner.dryrun`.
- Diff check: passed; Git reported only its configured LF-to-CRLF checkout
  notices for the two edited Markdown files.
- Working tree contained only the five declared issue-owned files.

## Evidence

Final complete host-harness log:

- Path:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-004-final-host.log`
- UTC start: `2026-07-26T03:49:45.8814784Z`
- UTC end: `2026-07-26T03:53:28.5167183Z`
- Size: 372,118 bytes; 5,819 lines.
- SHA-256:
  `7D8B8A4733BDFED498616F03FC933F81B43CC05AD54FE4AF1E885934E0D3CEB3`
- `TEST_PASS` lines: 2,907.
- `TEST_FAIL` lines: 0.
- Standard-error bytes: 0.
- `CLROS_M01_004_READY`: exactly 1.
- `TEST_PASS m01.004`: exactly 1.
- `TEST_PASS host.tests`: exactly 1.

Focused terminal markers:

```text
TEST_START m01.004
TEST_PASS m01.004 lifetime statuses and pod layouts are stable
TEST_PASS m01.004 default values fail closed
TEST_PASS m01.004 minimum representative and multi guard layouts succeed
TEST_PASS m01.004 high address layout remains checked
TEST_PASS m01.004 pointer count and capacity fail before dereference
TEST_PASS m01.004 backing range and stack top failures are classified
TEST_PASS m01.004 canonical table failures retain nested evidence
TEST_PASS m01.004 backing topology and accounting are exact
TEST_PASS m01.004 canonical reservations remain ledger compatible
TEST_PASS m01.004 reserved ready active transitions succeed
TEST_PASS m01.004 repeated and illegal transitions fail closed
TEST_PASS m01.004 malformed layouts and results cannot report success
TEST_PASS m01.004 validation precedence is deterministic
TEST_PASS m01.004 evaluation is nonmutating deterministic and allocation free
TEST_PASS m01.004 source excludes hosted live memory and stack switch dependencies
TEST_PASS m01.004 exact sources compile under nostdlib efi
CLROS_M01_004_READY
TEST_PASS m01.004
```

Final source hashes before commit:

- `src/OS.Kernel/Boot/OwnedBootStackLayout.cs`:
  `EC2E776D49053152326D326BC5134D802258CE729F2EAE53B5C28A4F9D5C6636`
- `tests/OS.HostTests/OwnedBootStackLayoutTests.cs`:
  `0340E4F45255ED45131078D5F3EA3C6A15CD2379C592DD6093CE2AB5AE614C94`
- `docs/boot.md`:
  `B0EF4FD9DD69E1A95841E14F49BD896074A09B1496B42536D0E84924DC60D474`
- `docs/architecture.md`:
  `3779FE408FFE4185887446D4E99EC31D4176A297F188B723F5967CC22207A866`

Reference provenance:

- `references/SharpOS-main/OS/src/Boot/BootStackPool.cs`, local SHA-256
  `B7C80FDD76BB0D17CF87AB358CCC50F413601D8A1B8C0A6ABB22EE8D40D90E7C`,
  influenced explicit owned backing. Its static 4 MiB pool and raw-pointer
  ownership model were not copied.
- `references/SharpOS-main/OS/src/Boot/BootStackSwitch.cs`, local SHA-256
  `637DCFC91E3B9647A0F8FBD4800E6C973ED78C6E5F73E2F7C10770C000234808`,
  influenced one-shot ordering and exclusive-top alignment. Its live switch,
  mutable flag, raw pointers, continuation, and patching path were not copied.
- `references/RoseOS-master/Loader/src/MemoryMap.cs`, local SHA-256
  `CE90AE629B538E558B57B0BD315ED8B11DD559ED1A841368D109DE28D70B8B28`,
  was used only as negative-test input for capacity, overflow, ownership, and
  failure-continuation hazards.

Three independent read-only reviews covered production invariants, adversarial
test completeness, documentation boundaries, and reference hashes. Review
findings were resolved before the final harness run.

## Failures

- An intermediate enum edit briefly duplicated
  `ReservationTableRejected`, producing compiler error `CS0102`. The duplicate
  was removed before focused testing.
- Production review found that early exact-two failures initially predicted
  canonical validator statuses even though the validator had not run, a
  one-page backing reused the zero-length auxiliary status, and transition
  results synthesized table capacity. Early failures now retain
  `Uninitialized` nested evidence, zero and undersized backing are distinct,
  and transition-only results carry zero/uninitialized table evidence.
- Review also identified that a copyable immutable layout cannot reject replay
  from two stale snapshots. The tests now demonstrate that limitation; the
  docs define `RepeatedLifetimeTransition` only as a same-state request and
  assign authoritative replay rejection to M01-017.
- Adversarial review identified missing zero-page guard/usable vectors,
  incomplete failed-transition evidence assertions, narrow malformed-result
  mutation, and missing canonical-EFI/marker-location checks. All were added
  before the final run.
- No final build, focused-test, full-harness, naming, source-firewall, or
  dry-run command failed.

## Deferred work

This step proves only a host-tested physical layout and pure lifetime-edge
contract. It does not:

- Allocate EFI pages or prove `LoaderData`/loaded-image ownership.
- Insert a stack reservation into a live ledger.
- Map or unmap a guard page.
- Build page tables or enforce NX/W^X.
- Convert the physical top into a virtual target.
- Reserve the Windows x64 home space or construct a call frame.
- Read or write `RSP`, emit executable bytes, invoke a switch stub, preserve a
  continuation, or abandon the firmware stack.
- Prevent replay from a stale copied layout without authoritative coordination.
- Link the leaf into `src/OS/OS.csproj`, call it from `EfiEntry`, or coordinate
  it through `BootSequence`.
- Provide real QEMU/OVMF or guest evidence.
- Change a completion-gate status.

M01-005 owns the stack-switch contract, M01-017 owns serialized coordination,
M01-020 owns explicit EFI linkage, M01-021 owns live allocation/wiring, and
M01-022 owns real-QEMU proof.
