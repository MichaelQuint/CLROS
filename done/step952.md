# Step 952 - Shared SZ-array allocation and unexpected-vector host contract

## What changed

Implementation checkpoint for M04-008/#77, its separately tracked EFI
integration #523, and the host-only portion of M02-016/#56. The parent integrated
the independently accepted two-file #56 host contract and final 41-path #77
package. The complete combined host gate remains pending at this implementation
checkpoint; no array QEMU evidence is accepted here.

The array leaf shares the existing fixed allocator's private commit path,
original retained-page authority, 104-byte access value and 80-byte state.
It validates SZ-array metadata, signed length, component width, alignment,
overflow, capacity and aliases before writes. The same one-page cursor serves
fixed and array allocations. Existing status values remain stable, with three
array-specific rejections appended. Metadata authentication belongs to the
EFI adapter; a caller-supplied numeric token alone is not trusted metadata.

The EFI candidate adds the demonstrated Array length field/getter and two
default-fail-closed runtime exports. Its finite experiment combines two fixed
objects, an empty byte array, three longs and one byte. The five allocations
consume 160 bytes; 82 fixed/empty-byte filler pairs reach exactly 4,096 bytes
and 169 allocations. Before-bind and exhaustion branches reject without
changing the heap or bump state. The existing adapter retains the sole binder
and original allocation page. Expanded observation storage is 6,352 bytes;
the heap remains one page. All three array probe properties default false.

The unexpected-vector host slice builds and independently decodes the complete
bounded interrupt stream, models the captured state and validates panic output.
It covers pristine context preparation, full stream readback, the original
lease/context identity, formatted-byte accounting, UART FIFO/shift-register
drain behavior and deterministic rejection. Its 232-byte callback entry depth
is not a complete native call-chain stack bound. No unexpected-vector native
adapter, software interrupt, guest panic or owned-current-state proof is added.

## Why it changed

This advances the runtime prerequisites for first-party .NET workloads while
preserving the original retained-memory authority and fail-closed defaults.
It also prepares deterministic diagnostics needed before further boot/runtime
integration. Neither slice establishes general runtime or hosted .NET support.

## Files changed

- #56: the new `UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification`
  source/test pair, with host-only notes in `docs/diagnostics.md` and
  `docs/boot-order.md`.
- #77/#523: 39 changed paths from the 41-path reviewed package, including the
  four accepted host source/test files and released native/project/script/test
  changes. Owned `src/OS/Boot/EfiEntry.cs` and
  `tests/OS.HostTests/M01LoadedImageStackEfiEntryTests.cs` are unchanged.
- `.gitea/backlog/clros-issues.json`: only the existing M02-016 and M04-008 rows
  are reconciled; all 345 rows, dependencies and required marker arrays remain.
- `docs/nativeaot-nostdlib-limits.md`, `docs/efi-build.md`, `docs/qemu.md` and
  this newly reserved step. Existing accepted fixed-allocation boot evidence
  is also linked correctly in `docs/boot-order.md`.

## Tests run

- Complete focused fixed/array host modules: 20 starts, 21 passes, zero failures;
  actual ten/eleven-source no-stdlib closures have zero AssemblyRefs.
- Complete unexpected-vector host module: 11 starts, 12 passes, zero failures;
  actual twelve-source no-stdlib closure has zero AssemblyRefs. Normal Release
  HostTests build passed with zero warnings/errors.
- Complete helper-ledger, system-export compatibility, shim-boundary,
  instruction-publication and checked-port-I/O modules: 24 starts, 25 passes,
  zero failures, native exit 0 and empty stderr after the recorded narrow fixes.
- Complete new array EFI initializer: six starts, seven passes, zero failures,
  native exit 0 and empty stderr from 2026-10-03T02:24:28.814840Z through
  02:31:13.118369Z. Its 41 input files were unchanged during execution. The
  actual initializer ran the child POD controls and published default,
  positive, before-bind and exhaustion artifacts. Each artifact records
  80 Compile items, 93 raw inputs, 31 evaluated flags and zero AssemblyRefs;
  enabled-flag counts are respectively 0, 9, 10 and 10. These are builds and
  offline artifact tests, not executed EFI images.
- Seven complete selected legacy project-contract methods: seven starts,
  eight passes, zero failures, native exit 0 and empty stderr, from
  2026-10-03T02:40:03.955335Z through 02:40:22.434816Z. Their harness suppresses
  automatic initializers and invokes the actual named methods. This does not
  represent seven complete modules. Independent review accepts the narrow
  exact-prerequisite and reciprocal-exclusion repairs.
- Independent raw-transcript preparation: 576 synthetic checks passed;
  three valid CLI cases returned 0 and the deliberate wrong-lane case returned
  1. This checks the auditor, not a guest. Positive/before-bind/exhaustion
  grammars contain exactly 68/49/61 owned CRLF records.
- Complete focused catalog initializer after final source integration and the
  last catalog edit: 17 starts, 18 passes, zero failures, native exit 0 and empty
  stderr from 2026-10-03T02:52:59.056982Z through 02:53:25.381891Z. Catalog/test/
  sync-script input hashes were unchanged during execution.

## Evidence

Ignored evidence paths remain local; generated outputs are not committed.
The parent read and rehashed the completed artifact package's 112 files and
all 41 archived source snapshots. Independent linked-artifact review is pending.

| Evidence | SHA256 |
|---|---|
| Author `artifacts/issue77/completed-artifact-batch-v1/identities.json` |403C1EC1FCCF8EA832E68BA089CBAED2B03E20762BF1FC3AFECBC70793702807|
| Author `artifacts/issue77/native-dev/complete-attempt1/result.json` |5C55DF58B12384553140E34A80335A7248851F736255C63CA3DC1AA689997309|
| Same attempt `stdout.log` |CA1BE5CD980E818FAF1D3385F5FEE00F6C3D60ED9AA3BF7B2AB9CD84692CFEBE|
| Parent `issue77-runtime-independent-review/shim-order-repair/review.md` |68F0C96DEBE99932727FC8A9C0A4DBDA1A64853BC8D8DBE69A55BD4297C7AEC8|
| Parent `issue77-runtime-independent-review/scripts-v1/review.md` |1A7654C106816B9CFA813A01C15F27426AA38B4CD414CAE1914F2711735832D8|
| Parent `issue77-runtime-independent-review/legacy-final/review.md` |62FF66318F37B68D86E2832F5F6104E044D937D573715455424DAD397C044CBE|
| Parent `issue77-runtime-independent-review/final41/source-acceptance.md` |83411BAA7EAC24D12E74F2DB5EA9C7F363BA105CBAE84A36C7118B65D31C9D18|
| Author `artifacts/issue77/final/owned41-sha256.json` |D50C8D505DE5735021E1AC434393E0513619C5FFA6EF03863D92FA569CE52F8E|
| Parent `issue523-manifest-focused/attempt1/stdout.log` |A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9|
| Parent `issue523-raw-preparation/package-identities.json` |0038EAFFFD87271258FA39863FDFF448FC54BCD2F8C0839E989E69489D23F961|
| #56 independent host review |43742D55DC5726D0D18197B6F8143D697252000C6EE0A16442BB3AD36EA95C7F|

Parent paths above are under `artifacts/orchestration-20261001/`.
Author paths are in the isolated `m04-008-arrays` worktree. The #56 host pair
was copied byte-identically on 2026-10-03T01:43:10.121127Z; its source identity
is `9854D7511D984FAB9466651C344D918E526F4839A6140A00A3DF0DBDFE5360D3`
and test identity is
`E7593B0BCB48808CBA39D81DE37CF69C9D77738625133C9278B112E7DEAEBFA1`.
The #77 source copy completed on 2026-10-03T02:52:18.985449Z after parent
verification of all 227 independent composition-review inputs. The guarded
copy verified all 41 final hashes, preserved the #56 pair and recorded its
39 changed paths in `issue523-integration/source-integration.json`.

## Failures

- The isolated compiler experiment first demonstrated the missing Array length
  contract, then the two unresolved array exports. The preserved terminal-helper
  experiment resolved those exact dependencies but proved no returned allocation.
- An ordinary source-scanner diagnostic incorrectly expected the thin array
  wrapper to be rejected. The unchanged generic scanner admitted it; the failed
  assertion remains preserved and no scanner exception was introduced.
- Complete ledger checks exposed a comma instead of the existing semicolon
  helper-symbol separator and stale counts. The next run exposed the append-only
  export-order mismatch. Moving the unchanged two array declarations after the
  unchanged RhpNewFast declaration fixed the order. Both failed runs and their
  source snapshots remain; the final five-module rerun passed.
- Earlier focused test-literal, local harness-alias and evidence-packaging
  mistakes remain in their original attempts. No failed run is relabeled as
  successful or substituted for the final complete initializer.
- The first legacy-only harness lacked its actual QemuRunner dependency. The
  next run exposed six predicates that assumed XML position or the older
  diagnostic text. Both failures remain preserved; exact old/new condition
  membership and diagnostic matching pass in the final selected-method run.

## Deferred work

Commit the reviewed source and run one clean same-head solution build and full
host suite. Complete independent native review, then capture and accept fresh
default, heap, fixed, mixed-array, array-before-bind, array-exhaustion,
page-fault and double-fault guests, including metadata/ABI, raw output,
process lifecycle and disk accounting. #77/#523 remain open until that evidence
and the reviewed merge are complete. #56 remains open for its separately held
native integration and full callback stack proof.

## Evidence boundary

Gate 4 remains Fail. There is no general array/string support, reference store
or barrier, GC, refill/reclamation, real ExitBootServices, scheduler/threading,
hosted .NET/Kestrel, cloud readiness or production claim. A built helper's
success path and synthetic serial records do not prove guest execution.
