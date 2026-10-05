# Step 994 - First compiled GC-static store/read in the EFI guest

The default-off `EnableM04GcStaticRootMaterializationProbe` now registers the
original loaded image, materializes its single GC-static base, and retains the
root through the existing allocator. A genuine `static object` field has no
initializer/cctor. Separate compiled setter/getter/address methods access its
published cell. A real value object and the static base share one allocation
cursor. The existing generated `RhpAssignRef` helper performs the two admitted
stores: object, then null. The old checked-reference schedule is unchanged.

The first boot exposed the actual compiler's writable `.data` placement of the
section201 entry array. `ReadableSectionData` admits reads only within registered
non-executable row payloads; it grants no writes. Retained entry bytes are still
checked on root queries. Descriptors, method tables and the Object prefix keep
their immutable-metadata requirements. Writable destinations remain excluded
from every registered row extent.

Changed implementation surfaces: the GC-static and TypeManager host cores and
their tests; new `GCStaticRootMaterializationProbe.cs` and its default-false
diagnostic authority; `RhpNewFastAllocation.cs`, `CheckedReferenceBarrierAdapter.cs`,
`MinimalRuntime.cs`, `EfiEntry.cs`, `OS.csproj`; the EFI producer/verifier/smoke
scripts and the existing serializer targets' five helper-selection conditions.

Validation and retained evidence:

- Focused seven-module host build: zero warnings/errors. Run: 82 starts,
  83 passes, zero failures; logs `artifacts/issue548-efi/registered-entry-host2.*`.
  Includes the actual no-stdlib closure, writable-table publication/tampering,
  executable-table refusal, and all prior allocator/array/String cases.
- Fresh normal EFI producer/verifier and QEMU/OVMF smoke: 2026-10-05
  07:07:26-07:08:43 UTC, exit 0, empty stderr. Complete command and results are
  in `artifacts/issue548-efi/guest3/smoke.result.json`.
- Strict serial parser accepted 58 CLROS records, two allocations/48 bytes,
  two checked stores, and `OS_BOOT_OK`/`OS_SMOKE_PASS`. Actual logs and runner
  summary are retained in `artifacts/issue548-efi/guest3`.
- EFI artifact `artifacts/efi/m04-gc-guest3.EFI`, SHA-256
  `2b4be2d3a344a42e3553c12ed8bcc021c5dd4ee53e27917f87ee1ba84287e643`.
  Its producer bound 98 Compile entries and 112 raw inputs. The checked helper
  object is unchanged. The earlier emitted setter/getter inspection joined
  the same cell with the field at +8 and the setter call to `RhpAssignRef`.

Failures retained: guest1 stopped before QEMU on duplicate marker arguments
caused by inconsistent trailing whitespace; guest2 exposed the writable entry
table assumption. Both were corrected before guest3. The focused no-stdlib
fixture also needed to stop appending a core now explicitly included by OS.csproj.

Deferred: complete coordinator/stack acceptance, complete integrated regression,
and PR547/issue closure. This is a development milestone, not final
merge acceptance. It proves a bounded noncollecting retained static root and
compiled field operations; it does not prove collection, general static/TLS
initialization, hosted .NET, real firmware exit, Kestrel, or cloud readiness.

Follow-up within this implementation slice:

- Refreshed the 31 existing compatibility consumers and four M01 profile
  selectors; all four complete M01 initializers pass (51 groups). Added three
  focused GC integration groups for default-off authority, strict transcript
  numeric/missing/duplicate/noise rejection, and the actual marker streamer
  with default isolation and invalid profile controls. Their separate final
  run passes 3 starts/4 passes/zero failures, with a clean build.
- Added all 33 reciprocal conflict declarations and updated the producer's
  exact guard allowlist. The first post-change capture (guest4) stopped at
  that allowlist before native publication. Guest5 passed after correction:
  07:39:47-07:41:32 UTC, exit0/empty stderr, same EFI hash as guest3.
- Fresh default capture passed 07:50:25-07:51:56 UTC, exit0/empty stderr;
  evidence is under `artifacts/issue548-efi/default-guest`. Both captures use
  the normal fresh producer/image/runner path.
- The first parser fixture run omitted the imported prerequisite marker
  constants. Loading the actual top-level definitions corrected the fixture;
  production parser behavior was unchanged. Failed logs are preserved.
- Native archive/review is under `artifacts/issue548-efi/native-enabled`. All
  535 emitted method extents match their COFF relocations and linked PE bytes.
  All three unchanged reference thunks bind to the new callback; the compiled
  Set/Get/Address roots share one cell and field offset +8. Actual GCDesc is
  (-16,8,1), with a 24-byte abbreviated static MT and writable cell. The emitted
  materializer writes the retained 600-byte record before cell publication and
  calls an independent readback before comparing the stored reference.
- The callback's 78-method graph has an 8504-byte bound below callback entry;
  its direct-call alignment, restored GPRs, volatile SIMD use and direct stack
  stores have been checked. Materialization's conservative 78-method graph,
  including both array/String switch tables, has a 9128-byte entry-relative
  bound. These are partial native review results, not a complete executing
  stack/canary or final native acceptance claim.

Validation follows the user's implementation-first policy: reuse the accepted
host checks and unchanged helper evidence, add focused behavior controls, and
run one complete common gate for the integrated merge wave. Native publication
and guest execution are recorded above rather than duplicated in a new test
harness. The six numeric stages reuse the strict existing heap transcript;
there is no separate decorative readiness line.
