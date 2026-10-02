# Step 935 - Retained heap-page host contract

## Summary

Integrate the independently reviewed #75 / M04-006 host leaf. Reserve one
4096-byte page using the existing mutator and the original allocator authority,
zero/read its explicit mapped backing, and retain it. Keep #75 open for the
separately claimed EFI handoff and source-bound guest acceptance.

## Why

Scalar heap-budget planning and the M04-005 layout contract do not reserve
storage. This leaf supplies the bounded one-page ownership and readback
prerequisite without adding object allocation, GC or a second allocator.
Retention validates current free-list exclusion/accounting, so valid later
allocation and release of a later page do not invalidate the retained page.

## Files changed

- `src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.cs`: two status-coded
  operations, stable original storage/parent identities, full-span preflight,
  one-page allocation, private zero/readback and read-only retention.
- `tests/OS.HostTests/ManagedHeapPageRangeLiveAllocatorTests.cs`: independent
  physical/backing fixtures, complete storage snapshots, hostile boundaries,
  retention/content separation, allocation measurement and no-stdlib metadata.
- `docs/nativeaot-nostdlib-limits.md` and `docs/completion-gates.md`: supported
  host behavior and pending guest/full-gate boundary.
- This step, allocated with `scripts/new-done-step.ps1 -Reserve`.

No allocator, M01 predicate, runtime shim, EFI, project, script, BootSequence,
Probes, reference source or existing test file changes belong to this leaf.

## Tests run

- Author actual-kernel-reference focused Release build/run: exit 0; normal
  HostTests Release project build: zero warnings/errors. Full harness not run.
- Positive cases use actual aligned backing, separate synthetic physical
  addresses, literal first-page/accounting expectations, an offset allocation,
  exact one-node authority, one-page exhaustion and high physical addresses.
- A later actual allocation is distinct. Retention passes while exhausted and
  after strict-LIFO freeing only that later page, preserving all storage.
- All 4096 retained-byte positions are separately changed to nonzero and
  observed through the same actual reader; ownership remains valid. Repeated
  initialization cannot clear the changed page or mutate allocator/heap state.
- Invalid spans, all ten pairwise aliases, overflowing capacities, malformed
  parent/topology/accounting, changed identities and retained free-list overlap
  reject without changes to the complete 65536-byte fixture. Whole supplied
  backing and metadata capacities are protected, including unused tails.
- POD checks and 1000 warmed reserve/retain/reject iterations allocate zero
  managed bytes. Private fixed-page zero/readback adds no generic utility.
- The actual eight repository-source no-stdlib closure compiles using existing
  MinimalRuntime/MethodImpl files with zero AssemblyReferences. The actual
  `ReadFixedPage` has `NoInlining` and a nonempty 46-byte IL body.
- Independent reviewer verified both source hashes, all 19 author evidence
  records, eight no-stdlib inputs and actual PE metadata; no findings.
- Parent copied only the reviewed source/test pair and verified both raw
  identities. Fresh parent focused build: exit 0, zero warnings/errors in
  8.69 seconds. Actual focused module: one start, two passes including terminal
  `TEST_PASS m04.006.kernel-focused`, no failures, exit 0, empty build/test
  stderr. State/result sizes are 216/160 bytes. The full settled-wave gate and
  fresh guest runs remain pending.

## Evidence

Author worktree: `C:/Users/Roger/.codex/worktrees/m04-006-heap/CLROS`, base
`b193f590c30e95dc1f7b3b156aafd13722889f65`. Ignored author evidence is under
`artifacts/m04-006`. Parent copies are under
`artifacts/orchestration-20261001/issue75-host-handoff`; fresh parent logs are
`issue75-root-build.*` and `issue75-root-test.*` in that orchestration directory.
Parent no-stdlib output is under
`artifacts/m04-006/nostdlib/b80296787480459f97f12a0acc13ddf2`.

| Item | SHA-256 |
| --- | --- |
| Production source | 43CDF5AF449F7963F07B200EC494DA3D8538F1CEB2F01FB3DBE9C29EDEBCB070 |
| Tests | AED8F7C32F80C5E600ED837E37C5F6DE477324E57A9F9AA1AC0AEDF43ADF4BF6 |
| Author report | A439D3BEB13E452AB271FE37B5872480E7866903B073FB017FC35679C92F04F5 |
| Exact API/size handoff | 27BCA75006CF93AC2816F8B19A5617C362C667A6A1411BC1465C6357B788FACD |
| Independent host review | 33F0DFB1AF1819FFA98BFB3FC292A5CBD67187599FF264BDB8A036B98D194291 |
| Parent focused stdout | 1A1559CCEB38F0824E3222DB6B7F6CE40DEEA13670BCF773EEAFE62CAE4E33FA |
| Parent focused build stdout | 395CD8100E86DF79B60B03BF019E03FA03E06CFB6F2B722856C723DA5C6FCC7D |
| Parent focused loaded kernel | 54EFAF5E40E98155C6821A2C533F3F3987E88E4A87CDC1AC6FDC9B82E7F23A78 |

## Failures

No author or parent build/test failures occurred. Direct PowerShell invocation
of the step allocator was rejected by the session's signing policy before it
ran; the documented explicit `powershell.exe -ExecutionPolicy Bypass -File`
invocation then reserved step935 successfully.

The controlled ReservedNotReady fixture represents an already reserved nonready
slot. It does not inject or prove a real failed memory write, hardware fault or
concurrent writer. Numeric pointer checks cannot recover unmapped-pointer
faults. Post-allocation failure branches retain ownership and never free or
restore the page/parent.

## Deferred work

The reviewed EFI supplement may be released after this host leaf is reviewed
and committed. Freeze exact API-to-record mappings, the ready-only ancillary
block and terminal profile before implementation. The expected closure is
70 Compile items / 83 source-authority inputs; these are planned counts only.
The parent owns shared-lane coordination, final docs, full gate, fresh guest
tuples, review/merge and tracker reconciliation. #75 stays open until that proof
is accepted and merged.

## Evidence boundary

This is host caller-buffer and compiler/metadata evidence. Original stable
exclusive authority and truthful backing mapping are caller preconditions.
Current free-list exclusion is not an unforgeable allocation registry,
clone/ABA protection, physical-alias detection or SMP safety. The full ledger
is revalidated and never written; only the selected parent is retained as an
exact value, not a historical snapshot of every other ledger entry.
No managed objects, allocation helpers, GC/roots/barriers, general free/reset,
multi-page allocator, hardware NX, permanent paging, post-firmware lifetime,
Kestrel/cloud readiness or numbered completion gate is established.
